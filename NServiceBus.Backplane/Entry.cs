namespace NServiceBus.Backplane
{
    public class Entry
    {
        public Entry(string owner, string type, string data)
        {
            Owner = owner;
            Type = type;
            Data = data;
        }

        public string Owner { get; set; }
        public string Type { get; }
        public string Data { get; }
    }
}