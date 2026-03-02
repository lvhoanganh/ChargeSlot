namespace ChargeSlot.Api.Helpers
{
    public static class RoleConstants
    {
        public const string Driver = "Driver";
        public const string Owner = "Owner";
        public const string Admin = "Admin";

        public static readonly HashSet<string> DbRoles = new()
        {
            Driver,
            Owner,
            Admin
        };

        public static readonly HashSet<string> Allowed = DbRoles;
    }
}
