namespace NServiceBus.Backplane
{
    /// <summary>
    /// Allows to cease the entry change subscription.
    /// </summary>
    public interface IDataBackplaneSubscription
    {
        /// <summary>
        /// Terminates the subscription.
        /// </summary>
        void Unsubscribe();
    }
}