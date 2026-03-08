namespace ChargeSlot.Api.Constants
{
    public static class RoleConstants
    {
        public const string Driver = "Driver";
        public const string Owner = "Owner";
        public const string Admin = "Admin";

        // Chỉ seed Driver, Owner vào DB — Admin config trong appsettings.json
        public static readonly HashSet<string> DbRoles = new()
        {
            Driver,
            Owner
        };

        // Chỉ cho phép đăng ký Driver hoặc Owner
        public static readonly HashSet<string> Allowed = DbRoles;
    }
}
