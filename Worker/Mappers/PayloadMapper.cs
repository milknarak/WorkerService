using Worker.Aggregates;
using Worker.Models;

namespace Worker.Mappers
{
    public static class PayloadMapper
    {
        private static readonly Dictionary<string, (string Debit, string Credit)> AccountMap = new()
        {
            { "ap_tax",             ("1342.001", "4370.001") },
            { "ap_tax_okc",         ("1342.002", "4370.001") },
            { "ap_tax_omp",         ("1660.001", "4370.001") },
            { "ap_road",            ("1342.003", "4370.001") },
            { "ap_fund",            ("1342.004", "4370.001") },
            { "ap_shipping",        ("1342.006", "4370.001") },
            { "ap_chemical_dosing", ("1342.012", "4370.001") },
            { "ap_lab_test",        ("1342.009", "4370.001") },
            { "ap_fuel",            ("1370.001", "4011.001") },
            { "ap_transport",       ("1342.005", "4021.001") },
            { "ap_other",           ("1342.005", "4022.001") },
            { "ap_debit_note",      ("1480.002", "7862.001") }
        };

        public static SapPayload Map(TransactionAggregate t, string type)
        {
            if (type.Equals("AP", StringComparison.OrdinalIgnoreCase))
                return MapAP(t);

            if (type.Equals("AR", StringComparison.OrdinalIgnoreCase))
                return MapAR(t);

            throw new Exception($"Unknown transaction type: {type}");
        }

        private static SapPayload MapAP(TransactionAggregate t)
        {
            var header = t.ApTransaction;
            var subs = t.ApSubTransaction ?? new List<ApSubTransactionRecord>();
            var today = DateTime.Today;

            var apTransaction = BuildApHeader(header, today);
            apTransaction.apSubTransaction = subs.Select(BuildApLineItem).ToList();
            apTransaction.apTransactionAcc = BuildApAccountingEntries(subs);
            apTransaction.apTransactionPurcTax = BuildApPurcTax(header, today);

            return new SapPayload { apTransaction = apTransaction };
        }

        private static ApTransaction BuildApHeader(ApTransactionRecord h, DateTime today)
        {
            return new ApTransaction
            {
                ou_code = "PTL",
                system_id = "API",
                local_type = h.local_type,
                doc_type = "IV",
                ap_code = h.vendor_code,
                tran_date = today,
                credit_code = "",
                due_date = h.due_date ?? today.AddDays(30),
                ref_inv_no = h.ref_inv_no,
                ref_inv_date = h.ref_inv_date ?? today,
                ref_doc_no = h.ref_doc_no,
                ref_po_no = h.ref_po_no,
                ref_gr_no_by_in = h.ref_gr_no_by_in,
                curr_code = h.curr_code,
                pre_curr_amt = h.pre_curr_amt,
                curr_amt = h.curr_amt,
                exchange_rate = h.exchange_rate,
                local_amt = h.curr_amt,
                remark = "",
                is_manual_acc = "TRUE",
                cr_by = "API",
                cr_date = DateTime.Now,
                prog_id = "API_PROCESS",
                upd_by = "API",
                upd_date = DateTime.Now,
                upd_prog_id = "API_PROCESS"
            };
        }

        private static ApSubTransaction BuildApLineItem(ApSubTransactionRecord s)
        {
            var (debit, _) = GetAccounts(s.sub_group_type);

            return new ApSubTransaction
            {
                tran_seq = s.seq,
                rev_exp_code = debit,
                div_code = "PTL",
                ou_det = "00000",
                curr_amt = s.curr_amt,
                local_amt = s.local_amt,
                note = s.remark ?? ""
            };
        }

        private static List<ApTransactionAcc> BuildApAccountingEntries(List<ApSubTransactionRecord> subs)
        {
            var entries = new List<ApTransactionAcc>();
            var seq = 1;

            foreach (var s in subs)
            {
                var (debit, credit) = GetAccounts(s.sub_group_type);

                entries.Add(new ApTransactionAcc
                {
                    acc_seq = seq++,
                    acc_code = debit,
                    div_code = "PTL",
                    ou_det = "00000",
                    dr_amt = s.curr_amt,
                    cr_amt = 0
                });

                entries.Add(new ApTransactionAcc
                {
                    acc_seq = seq++,
                    acc_code = credit,
                    div_code = "PTL",
                    ou_det = "00000",
                    dr_amt = 0,
                    cr_amt = s.curr_amt
                });
            }

            return entries;
        }

        private static List<ApTransactionPurcTax> BuildApPurcTax(ApTransactionRecord h, DateTime today)
        {
            if (string.IsNullOrWhiteSpace(h.tax_id))
                return new List<ApTransactionPurcTax>();

            return new List<ApTransactionPurcTax>
            {
                new ApTransactionPurcTax
                {
                    purc_seq = 1,
                    branch_code = "00000",
                    tax_id = h.tax_id,
                    branch_yn = "",
                    branch_name = "",
                    tax_status = "N",
                    purc_type = "IV",
                    purc_code = "P07-01",
                    purc_tax_no = h.ref_inv_no,
                    purc_tax_date = h.ref_inv_date ?? today,
                    vat_rate = 0,
                    vat_amt = 0,
                    total_amt = h.curr_amt ?? 0
                }
            };
        }

        private static (string Debit, string Credit) GetAccounts(string subGroupType)
        {
            return AccountMap.TryGetValue(subGroupType ?? "", out var pair)
                ? pair
                : ("", "");
        }

        private static SapPayload MapAR(TransactionAggregate t)
        {
            var payload = new SapPayload();
            var today = DateTime.Today;

            payload.arTransaction = new ArTransaction
            {
                ou_code = "PTL",
                ar_code = t.ArTransaction.vendor_code,
                tran_date = t.ArTransaction.tran_date ?? today,
                credit_code = "CR000",
                due_date = t.ArTransaction.due_date ?? today.AddDays(30),
                ref_doc_no = t.ArTransaction.ref_doc_no,
                ref_doc_date = t.ArTransaction.ref_doc_date,
                curr_code = "THB",
                exchange_rate = t.ArTransaction.exchange_rate,
                curr_amt = t.ArTransaction.curr_amt,
                local_amt = t.ArTransaction.local_amt,
                vat_amt = t.ArTransaction.vat_amt,
                remark = t.ArTransaction.remark,
                system_id = "AR",
                branch_code = "00000",
                is_manual_acc = (t.ArTransactionAcc?.Any() == true) ? "TRUE" : "FALSE",
                cr_by = "API",
                prog_id = "API"
            };

            payload.arTransaction.arSubTransaction =
                t.ArSubTransaction?.Select(s => new ArSubTransaction
                {
                    tran_seq = s.seq,
                    rev_exp_code = "1011.001",
                    div_code = "PTL",
                    ou_det = "00000",
                    curr_amt = s.curr_amt,
                    local_amt = s.local_amt
                }).ToList() ?? new();

            payload.arTransaction.arTransactionAcc =
                t.ArTransactionAcc?.Select(a => new ArTransactionAcc
                {
                    acc_seq = a.seq,
                    acc_code = a.acc_code,
                    div_code = "PTL",
                    ou_det = "00000",
                    dr_amt = a.dr_amt,
                    cr_amt = a.cr_amt
                }).ToList() ?? new();

            return payload;
        }
    }
}
