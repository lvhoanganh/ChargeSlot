namespace ChargeSlot.Api.Constants
{
    public static class RoleConstants
    {
        public const string Driver = "Driver";
        public const string Owner = "Owner";
        public const string Admin = "Admin";

        // Seed Driver, Owner, Admin vào DB
        public static readonly HashSet<string> DbRoles = new()
        {
            Driver,
            Owner,
            Admin
        };

        // Chỉ cho phép đăng ký Driver hoặc Owner
        public static readonly HashSet<string> Allowed = DbRoles;
    }
}
