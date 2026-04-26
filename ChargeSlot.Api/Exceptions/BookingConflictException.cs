namespace ChargeSlot.Api.Exceptions
{
    public class BookingConflictException : Exception
    {
        public List<string> Conflicts { get; }

        public BookingConflictException(string message, List<string> conflicts) : base(message)
        {
            Conflicts = conflicts;
        }
    }
}
