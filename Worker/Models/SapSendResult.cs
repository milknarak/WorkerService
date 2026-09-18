namespace Worker.Models
{
    // ผลการส่งเข้า ERP — แยก "สำเร็จ / ซ้ำอยู่แล้ว / error อื่น" ออกจากกัน
    // เพื่อให้ ProcessService ตัดสินใจได้ว่าจะ mark sent, mark ซ้ำ, หรือ retry
    public class SapSendResult
    {
        public bool Success { get; set; }

        // processErrMsg จาก ERP (เก็บลง send_failed_message) — null เมื่อสำเร็จ
        public string? ErrorMessage { get; set; }

        // ERP ตอบว่า "already in the system" = ข้อมูลเข้า ERP ไปแล้วจริง (ไม่ใช่ error ที่ต้อง retry)
        public bool IsAlreadyInSystem { get; set; }

        public static SapSendResult Ok() => new() { Success = true };

        public static SapSendResult Failed(string? message, bool alreadyInSystem = false) =>
            new() { Success = false, ErrorMessage = message, IsAlreadyInSystem = alreadyInSystem };
    }
}
