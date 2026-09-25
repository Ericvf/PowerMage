using System.Net;

namespace PowerMage.Services;

public sealed record HomeWizardDevice(
    string Serial,
    string ProductType,
    string ProductName,
    bool ApiEnabled,
    string ApiPath,
    string ServiceName,
    string HostName,
    int Port,
    IReadOnlyList<IPAddress> Addresses);
