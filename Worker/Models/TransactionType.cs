namespace Worker.Models
{
    public enum TransactionType
    {
        Ap,
        Ar
    }

    public static class TransactionTypeExtensions
    {
        /// <summary>
        /// แปลงค่า type ดิบจาก PocketBase ("AP"/"AR", ไม่สน case) เป็น enum.
        /// คืน false ถ้าไม่รู้จัก — ให้ caller log แล้ว skip
        /// </summary>
        public static bool TryParse(string? value, out TransactionType type)
        {
            switch (value?.Trim().ToUpperInvariant())
            {
                case "AP":
                    type = TransactionType.Ap;
                    return true;
                case "AR":
                    type = TransactionType.Ar;
                    return true;
                default:
                    type = default;
                    return false;
            }
        }
    }
}
