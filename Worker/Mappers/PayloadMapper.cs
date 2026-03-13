using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Worker.Aggregates;
using Worker.Models;

namespace Worker.Mappers
{
    public static class PayloadMapper
    {
        private static readonly Dictionary<(string subType, string localType), string> RevExpMap = new()
        {
            { ("ap_tax", ""), "1342.001" },
            { ("ap_tax_okc", ""), "1342.002" },
            { ("ap_tax_omp", ""), "1660.001" },
            { ("ap_road", ""), "1342.003" },
            { ("ap_fund", ""), "1342.004" },
            { ("ap_shipping", ""), "1342.006" },
            { ("ap_chemical_dosing", ""), "1342.012" },
            { ("ap_lab_test", ""), "1342.009" },
            { ("ap_fuel", ""), "1370.001" },
            { ("ap_transport", "L"), "1342.005" },
            { ("ap_transport", "O"), "1342.005" }
        };

        public static SapPayload Map(TransactionAggregate t, string type, string subType)
        {
            if (type.Equals("AP", StringComparison.OrdinalIgnoreCase))
                return MapAP(t, subType);

            if (type.Equals("AR", StringComparison.OrdinalIgnoreCase))
                return MapAR(t);

            throw new Exception($"Unknown transaction type: {type}");
        }

        private static SapPayload MapAP(TransactionAggregate t, string subType)
        {
            var payload = new SapPayload();
            var today = DateTime.Today;

            decimal? currAmt = t.ApTransaction.curr_amt;
            decimal vatRate = 0.10m; // 10%


            if (subType == "ap_transport" && currAmt.HasValue)
            {
                currAmt = Math.Round(currAmt.Value / (1 + vatRate), 2);
            }

            var revExpCode = GetRevExpCode(subType, t.ApTransaction.local_type);

            payload.apTransaction = new ApTransaction
            {
                ou_code = "PTL",
                system_id = "API",
                local_type = t.ApTransaction.local_type,
                doc_type = "IV",
                ap_code = t.ApTransaction.vendor_code,
                tran_date = today,
                credit_code = t.ApTransaction.credit_code,
                due_date = t.ApTransaction.due_date ?? today.AddDays(30),
                ref_inv_no = t.ApTransaction.ref_inv_no,
                ref_inv_date = t.ApTransaction.ref_inv_date ?? today,
                ref_doc_no = t.ApTransaction.ref_doc_no,
                ref_po_no = t.ApTransaction.ref_po_no,
                ref_gr_no_by_in = t.ApTransaction.ref_gr_no_by_in,
                curr_code = t.ApTransaction.curr_code,
                pre_curr_amt = Math.Round(currAmt.Value / (1 + vatRate), 2),
                curr_amt = currAmt,
                exchange_rate = t.ApTransaction.exchange_rate,
                local_amt = currAmt * t.ApTransaction.exchange_rate,
                remark = t.ApTransaction.remark,
                is_manual_acc = (t.ApTransactionAcc?.Any() == true) ? "TRUE" : "FALSE",
                cr_by = "API",
                cr_date = DateTime.Now,
                prog_id = "API_PROCESS",
                upd_by = "API",
                upd_date = DateTime.Now,
                upd_prog_id = "API_PROCESS"
            };

            payload.apTransaction.apSubTransaction =
                t.ApSubTransaction?.Select(s => new ApSubTransaction
                {
                    tran_seq = s.seq,
                    rev_exp_code = revExpCode,
                    div_code = "PTL",
                    ou_det = "00000",
                    curr_amt = s.curr_amt,
                    local_amt = currAmt * t.ApTransaction.exchange_rate,
                    note = ""
                }).ToList() ?? new();

            payload.apTransaction.apTransactionPurcTax =
                t.ApTransactionPurcTax?.Select(x => new ApTransactionPurcTax
                {
                    purc_seq = x.seq,
                    branch_code = "00000",
                    tax_id = x.tax_id,
                    branch_yn = "",
                    branch_name = x.branch_name,
                    tax_status = "N",
                    purc_type = "IV",
                    purc_code = "P07-01",
                    purc_tax_no = x.purc_tax_no,
                    purc_tax_date = x.purc_tax_date ?? today,
                    vat_rate = x.vat_rate,
                    vat_amt = x.vat_amt,
                    total_amt = x.total_amt
                }).ToList() ?? new();

            payload.apTransaction.apTransactionAcc =
                t.ApTransactionAcc?.Select(a => new ApTransactionAcc
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

        private static string GetRevExpCode(string subType, string localType)
        {
            var key = (subType, localType ?? "");

            return RevExpMap.TryGetValue(key, out var code)
                ? code
                : "";
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
