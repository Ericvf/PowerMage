namespace PowerMage.Services;

public interface IHomewizardDiscovery
{
    event EventHandler<HomeWizardDevice>? DeviceDiscovered;

    Task Discover(CancellationToken cancellationToken);
}
