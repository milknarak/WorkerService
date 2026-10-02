namespace Worker.Models
{
    // collection ap_parameter — เก็บค่า parameter ทั่วไปแบบ key/value
    // ใช้งานตอนนี้: running no ของ ref_inv_no สำหรับ ap_debit_note (เก็บเลขล่าสุดแบบเต็มใน description เช่น "IMIF26/00007")
    public class ApParameterRecord
    {
        public string id { get; set; }
        public string parameter_code { get; set; }
        public string parameter_name { get; set; }
        // สำหรับ running no: เก็บเลขที่ออกไปล่าสุดแบบเต็ม (IMIFYY/xxxxx) — ประกอบทั้งปี + ตัวนับไว้ในค่าเดียว
        public string? description { get; set; }
        public bool active { get; set; }
    }
}
