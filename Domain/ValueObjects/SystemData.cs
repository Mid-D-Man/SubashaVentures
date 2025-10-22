// Domain/ValueObjects/SystemData.cs

using SubashaVentures.Utilities.HelperScripts;

namespace SubashaVentures.Domain.ValueObjects
{
    public readonly record struct ValidationEventArgs
    {
        public ValidationEventArgs()
        {
            Value = null;
            FieldName = null;
        }

        public string Value { get; init; }
        public string FieldName { get; init; }
        public bool IsValid { get; init; } = true;
        public string ErrorMessage { get; init; } = string.Empty;
    }

    public  class ConnectivityStatus
    {
        public ConnectivityStatus()
        {
            IsOnline = false;
            LastOnlineTime = null;
        }

        public bool IsOnline { get; init; }
        public string NetworkQuality { get; init; } = "unknown";
        public string ConnectionStability { get; init; } = "unknown";
        public DateTime? LastOnlineTime { get; init; }
    }

    public readonly record struct ConnectionInfo
    {
        public string? EffectiveType { get; init; }
        public double? Downlink { get; init; }
        public int? Rtt { get; init; }
        public bool SaveData { get; init; }
    }

    public class ConnectivityReport : ConnectivityStatus
    {
        public bool HasConnection { get; init; }
        public List<bool> ConnectionHistory { get; init; } = new();
        public ConnectionInfo? ConnectionInfo { get; init; }

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }
    
    
}