namespace ChargeSlot.Api.Helpers
{
    public static class RoleConstants
    {
        public const string Driver = "Driver";
        public const string Owner = "Owner";
        public const string Admin = "Admin";

        public static readonly HashSet<string> Allowed = new()
        {
            Driver, Owner, Admin
        };
    }
}
