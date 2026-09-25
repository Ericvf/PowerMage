using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace PowerMage.Services;

public sealed class HomewizardDiscovery : IHomewizardDiscovery
{
    private const string ServiceType = "_hwenergy._tcp.local";
    private const string MdnsAddress = "224.0.0.251";
    private const int MdnsPort = 5353;

    public event EventHandler<HomeWizardDevice>? DeviceDiscovered;

    public async Task Discover(CancellationToken cancellationToken)
    {
        var interfaces = NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(x =>
                x.OperationalStatus == OperationalStatus.Up &&
                x.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                x.SupportsMulticast)
            .ToList();

        var tasks = new List<Task>();

        foreach (var networkInterface in interfaces)
        {
            foreach (var address in networkInterface
                .GetIPProperties()
                .UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                tasks.Add(
                    DiscoverInterfaceAsync(
                        address.Address,
                        cancellationToken));
            }
        }

        await Task.WhenAll(tasks);
    }

    private async Task DiscoverInterfaceAsync(
        IPAddress localAddress,
        CancellationToken cancellationToken)
    {
        using var socket = new Socket(
            AddressFamily.InterNetwork,
            SocketType.Dgram,
            ProtocolType.Udp);

        socket.SetSocketOption(
            SocketOptionLevel.Socket,
            SocketOptionName.ReuseAddress,
            true);

        socket.Bind(
            new IPEndPoint(
                localAddress,
                MdnsPort));

        socket.SetSocketOption(
            SocketOptionLevel.IP,
            SocketOptionName.AddMembership,
            new MulticastOption(
                IPAddress.Parse(MdnsAddress),
                localAddress));

        socket.SetSocketOption(
            SocketOptionLevel.IP,
            SocketOptionName.MulticastInterface,
            localAddress.GetAddressBytes());

        var multicastEndpoint = new IPEndPoint(
            IPAddress.Parse(MdnsAddress),
            MdnsPort);

        // Browse for _hwenergy._tcp.
        await SendQueryAsync(
            socket,
            multicastEndpoint,
            ServiceType,
            type: 12);

        var buffer = new byte[9000];

        // State is deliberately maintained for the lifetime
        // of this interface's discovery session.
        var devices = new Dictionary<string, DeviceState>(
            StringComparer.OrdinalIgnoreCase);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                SocketReceiveFromResult result;

                try
                {
                    result = await socket.ReceiveFromAsync(
                        buffer,
                        SocketFlags.None,
                        new IPEndPoint(
                            IPAddress.Any,
                            0),
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                try
                {
                    ProcessPacket(
                        buffer.AsSpan(
                            0,
                            result.ReceivedBytes),
                        socket,
                        multicastEndpoint,
                        devices);
                }
                catch (Exception ex)
                {
                    // Don't let one malformed packet kill discovery.
                    Console.Error.WriteLine(
                        $"mDNS packet error: {ex.Message}");
                }
            }
        }
        finally
        {
            try
            {
                socket.SetSocketOption(
                    SocketOptionLevel.IP,
                    SocketOptionName.DropMembership,
                    new MulticastOption(
                        IPAddress.Parse(MdnsAddress),
                        localAddress));
            }
            catch
            {
                // Socket is being disposed anyway.
            }
        }
    }

    private void ProcessPacket(
        ReadOnlySpan<byte> packet,
        Socket socket,
        IPEndPoint multicastEndpoint,
        Dictionary<string, DeviceState> devices)
    {
        if (packet.Length < 12)
            return;

        ushort flags =
            BinaryPrimitives.ReadUInt16BigEndian(
                packet.Slice(2, 2));

        // We only care about responses.
        if ((flags & 0x8000) == 0)
            return;

        ushort questionCount =
            BinaryPrimitives.ReadUInt16BigEndian(
                packet.Slice(4, 2));

        ushort answerCount =
            BinaryPrimitives.ReadUInt16BigEndian(
                packet.Slice(6, 2));

        ushort authorityCount =
            BinaryPrimitives.ReadUInt16BigEndian(
                packet.Slice(8, 2));

        ushort additionalCount =
            BinaryPrimitives.ReadUInt16BigEndian(
                packet.Slice(10, 2));

        int offset = 12;

        // Skip questions.
        for (int i = 0; i < questionCount; i++)
        {
            ReadName(packet, ref offset);

            if (offset + 4 > packet.Length)
                return;

            offset += 4;
        }

        int recordCount =
            answerCount +
            authorityCount +
            additionalCount;

        for (int i = 0; i < recordCount; i++)
        {
            if (offset >= packet.Length)
                return;

            string name =
                ReadName(packet, ref offset);

            if (offset + 10 > packet.Length)
                return;

            ushort type =
                BinaryPrimitives.ReadUInt16BigEndian(
                    packet.Slice(offset, 2));

            ushort dnsClass =
                BinaryPrimitives.ReadUInt16BigEndian(
                    packet.Slice(offset + 2, 2));

            uint ttl =
                BinaryPrimitives.ReadUInt32BigEndian(
                    packet.Slice(offset + 4, 4));

            ushort dataLength =
                BinaryPrimitives.ReadUInt16BigEndian(
                    packet.Slice(offset + 8, 2));

            offset += 10;

            if (offset + dataLength > packet.Length)
                return;

            // IMPORTANT:
            // Keep the absolute packet offset.
            //
            // DNS compression pointers inside RDATA point to
            // offsets in the complete DNS packet.
            int dataOffset = offset;

            var data =
                packet.Slice(
                    dataOffset,
                    dataLength);

            // Only IN records.
            if ((dnsClass & 0x7FFF) != 1)
            {
                offset += dataLength;
                continue;
            }

            switch (type)
            {
                case 12: // PTR
                    ProcessPtr(
                        name,
                        ttl,
                        packet,
                        dataOffset,
                        socket,
                        multicastEndpoint,
                        devices);
                    break;

                case 16: // TXT
                    ProcessTxt(
                        name,
                        data,
                        devices);
                    break;

                case 33: // SRV
                    ProcessSrv(
                        name,
                        packet,
                        dataOffset,
                        dataLength,
                        devices);
                    break;

                case 1: // A
                    ProcessA(
                        name,
                        data,
                        devices);
                    break;

                case 28: // AAAA
                    ProcessAaaa(
                        name,
                        data,
                        devices);
                    break;
            }

            offset += dataLength;
        }

        // Records can arrive in any order.
        // Try publishing after processing the complete packet.
        PublishDevices(devices);
    }

    private void ProcessPtr(
        string name,
        uint ttl,
        ReadOnlySpan<byte> packet,
        int dataOffset,
        Socket socket,
        IPEndPoint multicastEndpoint,
        Dictionary<string, DeviceState> devices)
    {
        if (!name.Equals(
                ServiceType,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        int offset = dataOffset;

        string instance =
            ReadName(
                packet,
                ref offset);

        if (ttl == 0)
        {
            devices.Remove(instance);
            return;
        }

        if (!devices.TryGetValue(
                instance,
                out var device))
        {
            device = new DeviceState(instance);
            devices[instance] = device;

            // Explicitly request TXT.
            _ = SendQueryAsync(
                socket,
                multicastEndpoint,
                instance,
                type: 16);

            // Explicitly request SRV.
            _ = SendQueryAsync(
                socket,
                multicastEndpoint,
                instance,
                type: 33);
        }

        device.IsPresent = true;
    }

    private static void ProcessTxt(
        string name,
        ReadOnlySpan<byte> data,
        Dictionary<string, DeviceState> devices)
    {
        if (!name.EndsWith(
                ServiceType,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // TXT may arrive before PTR.
        if (!devices.TryGetValue(
                name,
                out var device))
        {
            device = new DeviceState(name);
            devices[name] = device;
        }

        int offset = 0;

        while (offset < data.Length)
        {
            int length = data[offset++];

            if (offset + length > data.Length)
                return;

            var entry =
                data.Slice(
                    offset,
                    length);

            offset += length;

            string text =
                Encoding.UTF8.GetString(entry);

            int equals =
                text.IndexOf('=');

            if (equals >= 0)
            {
                string key =
                    text[..equals];

                string value =
                    text[(equals + 1)..];

                device.Txt[key] = value;
            }
            else
            {
                device.Txt[text] = "";
            }
        }

        device.HasTxt = true;
    }

    private static void ProcessSrv(
        string name,
        ReadOnlySpan<byte> packet,
        int dataOffset,
        int dataLength,
        Dictionary<string, DeviceState> devices)
    {
        if (dataLength < 6)
            return;

        // SRV:
        //
        // priority  2 bytes
        // weight    2 bytes
        // port      2 bytes
        // target    DNS name
        //

        int offset = dataOffset;

        // Skip priority.
        offset += 2;

        // Skip weight.
        offset += 2;

        ushort port =
            BinaryPrimitives.ReadUInt16BigEndian(
                packet.Slice(offset, 2));

        offset += 2;

        string hostName =
            ReadName(
                packet,
                ref offset);

        // SRV may arrive before PTR.
        if (!devices.TryGetValue(
                name,
                out var device))
        {
            device = new DeviceState(name);
            devices[name] = device;
        }

        device.HostName = hostName;
        device.Port = port;
        device.HasSrv = true;
    }

    private static void ProcessA(
        string name,
        ReadOnlySpan<byte> data,
        Dictionary<string, DeviceState> devices)
    {
        if (data.Length != 4)
            return;

        var address =
            new IPAddress(data.ToArray());

        foreach (var device in devices.Values)
        {
            if (!string.Equals(
                    device.HostName,
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!device.Addresses.Contains(address))
                device.Addresses.Add(address);
        }
    }

    private static void ProcessAaaa(
        string name,
        ReadOnlySpan<byte> data,
        Dictionary<string, DeviceState> devices)
    {
        if (data.Length != 16)
            return;

        var address =
            new IPAddress(data.ToArray());

        foreach (var device in devices.Values)
        {
            if (!string.Equals(
                    device.HostName,
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!device.Addresses.Contains(address))
                device.Addresses.Add(address);
        }
    }

    private void PublishDevices(
        Dictionary<string, DeviceState> devices)
    {
        foreach (var state in devices.Values)
        {
            // TXT is the minimum information we need from
            // the HomeWizard API to identify the device.
            if (!state.HasTxt)
                continue;

            if (!state.Txt.TryGetValue(
                    "serial",
                    out var serial) ||
                string.IsNullOrWhiteSpace(serial))
            {
                continue;
            }

            // Don't publish the exact same state repeatedly.
            var currentDevice =
                CreateDevice(state, serial);

            if (state.LastPublished is not null &&
                state.LastPublished.Equals(currentDevice))
            {
                continue;
            }

            state.LastPublished = currentDevice;

            DeviceDiscovered?.Invoke(
                this,
                currentDevice);
        }
    }

    private static HomeWizardDevice CreateDevice(
        DeviceState state,
        string serial)
    {
        bool apiEnabled =
            state.Txt.TryGetValue(
                "api_enabled",
                out var api) &&
            api == "1";

        string path =
            state.Txt.TryGetValue(
                "path",
                out var p)
                ? p
                : "/api/v1";

        string productName =
            state.Txt.TryGetValue(
                "product_name",
                out var pn)
                ? pn
                : "";

        string productType =
            state.Txt.TryGetValue(
                "product_type",
                out var pt)
                ? pt
                : "";

        return new HomeWizardDevice(
            Serial: serial,
            ProductType: productType,
            ProductName: productName,
            ApiEnabled: apiEnabled,
            ApiPath: path,
            ServiceName: state.ServiceName,
            HostName: state.HostName ?? "",
            Port: state.Port,
            Addresses: state.Addresses.ToArray());
    }

    private static async Task SendQueryAsync(
        Socket socket,
        IPEndPoint endpoint,
        string name,
        ushort type)
    {
        try
        {
            byte[] packet =
                BuildQuery(
                    name,
                    type);

            await socket.SendToAsync(
                packet,
                SocketFlags.None,
                endpoint);
        }
        catch
        {
            // Network interface may have disappeared.
        }
    }

    private static byte[] BuildQuery(
        string name,
        ushort type)
    {
        using var stream = new MemoryStream();

        // DNS header.
        WriteUInt16(stream, 0); // Transaction ID
        WriteUInt16(stream, 0); // Flags

        WriteUInt16(stream, 1); // Questions
        WriteUInt16(stream, 0); // Answers
        WriteUInt16(stream, 0); // Authority
        WriteUInt16(stream, 0); // Additional

        WriteName(
            stream,
            name);

        WriteUInt16(
            stream,
            type);

        WriteUInt16(
            stream,
            1); // IN

        return stream.ToArray();
    }

    private static string ReadName(
        ReadOnlySpan<byte> packet,
        ref int offset)
    {
        var builder = new StringBuilder();

        int position = offset;
        int returnOffset = -1;

        bool jumped = false;

        for (int jumps = 0; jumps < 32; jumps++)
        {
            if (position >= packet.Length)
                throw new InvalidDataException(
                    "DNS name exceeds packet.");

            byte length =
                packet[position];

            // DNS compression pointer.
            if ((length & 0xC0) == 0xC0)
            {
                if (position + 1 >= packet.Length)
                    throw new InvalidDataException(
                        "Invalid DNS compression pointer.");

                ushort pointer =
                    (ushort)(
                        ((length & 0x3F) << 8) |
                        packet[position + 1]);

                if (!jumped)
                    returnOffset =
                        position + 2;

                jumped = true;
                position = pointer;

                continue;
            }

            // End of name.
            if (length == 0)
            {
                if (!jumped)
                    returnOffset =
                        position + 1;

                break;
            }

            // Normal label.
            position++;

            if (position + length > packet.Length)
                throw new InvalidDataException(
                    "DNS label exceeds packet.");

            if (builder.Length > 0)
                builder.Append('.');

            builder.Append(
                Encoding.UTF8.GetString(
                    packet.Slice(
                        position,
                        length)));

            position += length;
        }

        if (returnOffset < 0)
            throw new InvalidDataException(
                "Invalid DNS name.");

        offset = returnOffset;

        return builder.ToString();
    }

    private static void WriteName(
        Stream stream,
        string name)
    {
        foreach (string label in name.Split('.'))
        {
            byte[] bytes =
                Encoding.UTF8.GetBytes(label);

            if (bytes.Length > 63)
                throw new ArgumentException(
                    "DNS label is too long.");

            stream.WriteByte(
                (byte)bytes.Length);

            stream.Write(bytes);
        }

        stream.WriteByte(0);
    }

    private static void WriteUInt16(
        Stream stream,
        ushort value)
    {
        Span<byte> buffer =
            stackalloc byte[2];

        BinaryPrimitives.WriteUInt16BigEndian(
            buffer,
            value);

        stream.Write(buffer);
    }

    private sealed class DeviceState
    {
        public DeviceState(string serviceName)
        {
            ServiceName = serviceName;
        }

        public string ServiceName { get; }

        public bool IsPresent { get; set; }

        public bool HasTxt { get; set; }

        public bool HasSrv { get; set; }

        public Dictionary<string, string> Txt { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public string? HostName { get; set; }

        public int Port { get; set; }

        public List<IPAddress> Addresses { get; } = [];

        public HomeWizardDevice? LastPublished { get; set; }
    }
}
