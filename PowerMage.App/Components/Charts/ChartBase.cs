using Microsoft.AspNetCore.Components;
using PowerMage.Repository;

namespace PowerMage.Components.Charts
{
    public abstract class ChartBase : ComponentBase
    {
        [Parameter]
        public DateTime Day { get; set; }

        [Parameter]
        public DeviceModel? Device { get; set; }

        [Parameter]
        public Views View { get; set; } = Views.Day;

        public enum Views
        {
            Day,
            Week,
            Month,
            Year
        }

        protected int momentsPerView => View switch
        {
            Views.Day => 1440 / 5,
            Views.Week => 7,
            Views.Month => 31,
            Views.Year => 365,
            _ => 1440 / 5
        };

        protected int timeUnit => View switch
        {
            Views.Day => 60 * 5,
            _ => 60 * 60 * 24
        };

        protected int daysPerView => View switch
        {
            Views.Day => 1,
            Views.Week => 7,
            Views.Month => 31,
            Views.Year => 365,
            _ => 1
        };

        protected string labelFormat => View switch
        {
            Views.Day => "HH:mm",
            Views.Week => "dd/MM",
            Views.Month => "dd/MM",
            Views.Year => "dd/MM",
            _ => "HH:mm"
        };
    }
}
