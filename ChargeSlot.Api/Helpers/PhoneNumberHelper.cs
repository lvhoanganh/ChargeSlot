namespace ChargeSlot.Api.Helpers
{
    public static class PhoneNumberHelper
    {
        /// <summary>
        /// Chuẩn hóa số điện thoại: bỏ khoảng trắng, chuyển +84xxx về 0xxx.
        /// Đồng thời validate: chỉ cho phép số Việt Nam phổ biến (bắt đầu 0, độ dài 10–11).
        /// Ném InvalidOperationException nếu không hợp lệ.
        /// </summary>
        public static string NormalizeAndValidate(string rawPhone)
        {
            if (string.IsNullOrWhiteSpace(rawPhone))
                throw new InvalidOperationException("Vui lòng nhập số điện thoại.");

            // Bỏ khoảng trắng
            var phone = rawPhone.Trim().Replace(" ", "");

            // Chuyển +84xxxxxxxxx -> 0xxxxxxxxx
            if (phone.StartsWith("+84"))
            {
                phone = "0" + phone[3..];
            }

            // Chỉ cho phép ký tự số
            if (!phone.All(char.IsDigit))
                throw new InvalidOperationException("Số điện thoại chỉ được chứa chữ số.");

            // Độ dài phổ biến của số VN: 10 hoặc 11 số
            if (phone.Length is < 9 or > 11)
                throw new InvalidOperationException("Độ dài số điện thoại không hợp lệ.");

            // Bắt buộc bắt đầu bằng 0
            if (!phone.StartsWith('0'))
                throw new InvalidOperationException("Định dạng số điện thoại không hợp lệ.");

            return phone;
        }
    }
}

