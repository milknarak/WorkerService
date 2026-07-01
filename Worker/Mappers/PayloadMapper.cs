using System.Text.RegularExpressions;
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

        public static SapPayload Map(TransactionAggregate t, TransactionType type, DateTime now)
        {
            return type switch
            {
                TransactionType.Ap => MapAP(t, now),
                TransactionType.Ar => MapAR(t, now),
                _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unknown transaction type: {type}")
            };
        }

        private static SapPayload MapAP(TransactionAggregate t, DateTime now)
        {
            var header = t.ApTransaction;
            var subs = t.ApSubTransaction ?? new List<ApSubTransactionRecord>();
            var today = now.Date;

            // ลูกค้าลาว (LAK): ส่งเลขดิบ ให้ ERP แปลงค่าเงินเอง — local_amt = curr_amt
            // upstream เขียนยอดลงเฉพาะ sub, header curr_amt = ผลรวม sub
            var headerAmt = subs.Sum(s => s.curr_amt ?? 0);

            var apTransaction = BuildApHeader(header, now, headerAmt);
            apTransaction.apSubTransaction = subs.Select(BuildApLineItem).ToList();
            apTransaction.apTransactionAcc = BuildApAccountingEntries(subs);
            apTransaction.apTransactionPurcTax = BuildApPurcTax(header, today, t.Customer?.customer_name, headerAmt);

            return new SapPayload { apTransaction = apTransaction };
        }

        private static ApTransaction BuildApHeader(ApTransactionRecord h, DateTime now, decimal currAmt)
        {
            var today = now.Date;

            return new ApTransaction
            {
                ou_code = "PTL",
                system_id = "API",
                local_type = h.local_type,
                doc_type = "IV",
                adjust_reason_code = "",
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
                pre_curr_amt = currAmt,   // ไม่มี VAT ฝั่งเรา → ยอดก่อน VAT = ยอดเต็ม
                curr_amt = currAmt,
                exchange_rate = h.exchange_rate,
                local_amt = currAmt,
                remark = "",
                is_manual_acc = "TRUE",
                cr_by = "API",
                cr_date = now,
                prog_id = "API_PROCESS",
                upd_by = "API",
                upd_date = now,
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
                local_amt = s.curr_amt,
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
                var remark = s.remark ?? "";

                entries.Add(new ApTransactionAcc
                {
                    acc_seq = seq++,
                    acc_code = debit,
                    div_code = "PTL",
                    ou_det = "00000",
                    dr_amt = s.curr_amt,
                    cr_amt = 0,
                    remark = remark
                });

                entries.Add(new ApTransactionAcc
                {
                    acc_seq = seq++,
                    acc_code = credit,
                    div_code = "PTL",
                    ou_det = "00000",
                    dr_amt = 0,
                    cr_amt = s.curr_amt,
                    remark = remark
                });
            }

            return entries;
        }

        private static List<ApTransactionPurcTax> BuildApPurcTax(ApTransactionRecord h, DateTime today, string paymentName, decimal amount)
        {

            return new List<ApTransactionPurcTax>
            {
                new ApTransactionPurcTax
                {
                    purc_seq = 1,
                    branch_code = "00000",
                    tax_id = h.tax_id ?? "",
                    branch_yn = "",
                    branch_name = "",
                    tax_status = "N",
                    purc_type = "IV",
                    purc_code = "P07-01",
                    purc_tax_no = h.ref_inv_no,
                    purc_tax_date = h.ref_inv_date ?? today,
                    payment_name = paymentName ?? "",
                    gs_non_vat_amt = amount,
                    gs_amt = 0,
                    vat_rate = 0,
                    vat_amt = 0,
                    total_amt = amount
                }
            };
        }

        private static (string Debit, string Credit) GetAccounts(string subGroupType)
        {
            return AccountMap.TryGetValue(subGroupType ?? "", out var pair)
                ? pair
                : ("", "");
        }

        private const string AR_RECEIVABLE_ACC = "1210.001";
        private const string AR_REVENUE_ACC = "7070.001";
        private const string AR_VAT_ACC = "4320.001";
        private const decimal VAT_RATE = 0.10m;

        private static SapPayload MapAR(TransactionAggregate t, DateTime now)
        {
            var header = t.ArTransaction;
            var subs = t.ArSubTransaction ?? new List<ArSubTransactionRecord>();
            var today = now.Date;

            // เหมือน AP: upstream เขียนยอดลงเฉพาะ sub, header curr_amt = ผลรวม sub (ยอดรวม incl VAT)
            var headerAmt = subs.Sum(s => s.curr_amt ?? 0);
            // VAT รวม = ผลรวม VAT ต่อบรรทัด (คิดวิธีเดียวกับ GL เพื่อให้ยอดตรงกันเป๊ะ)
            var totalVat = subs.Sum(s => CalcVat(s.curr_amt ?? 0));

            var arTransaction = BuildArHeader(header, today, headerAmt, totalVat);
            arTransaction.arSubTransaction = subs.Select(BuildArLineItem).ToList();
            arTransaction.arTransactionAcc = BuildArAccountingEntries(headerAmt, subs);

            return new SapPayload { arTransaction = arTransaction };
        }

        private static ArTransaction BuildArHeader(ArTransactionRecord h, DateTime today, decimal currAmt, decimal vatAmt)
        {
            return new ArTransaction
            {
                ou_code = "PTL",
                ar_code = h.vendor_code,
                doc_type = "IV",
                adjust_reason_code = "",
                tran_date = today,
                credit_code = "CR000",
                due_date = h.due_date ?? today.AddDays(30),
                ref_doc_no = h.ref_doc_no,
                ref_doc_date = h.ref_doc_date ?? today,
                curr_code = h.curr_code,
                exchange_rate = h.exchange_rate,
                curr_amt = currAmt,
                local_amt = currAmt,
                vat_amt = vatAmt,
                remark = "",
                system_id = "AR",
                branch_code = "00000",
                is_manual_acc = "TRUE",
                cr_by = "API",
                prog_id = "API"
            };
        }

        private static ArSubTransaction BuildArLineItem(ArSubTransactionRecord s)
        {
            return new ArSubTransaction
            {
                tran_seq = s.seq,
                rev_exp_code = AR_REVENUE_ACC,
                div_code = "PTL",
                ou_det = "00000",
                curr_amt = s.curr_amt,
                local_amt = s.curr_amt,
                note = s.remark ?? ""
            };
        }

        private static List<ArTransactionAcc> BuildArAccountingEntries(decimal headerAmt, List<ArSubTransactionRecord> subs)
        {
            var entries = new List<ArTransactionAcc>();
            var seq = 1;

            entries.Add(new ArTransactionAcc
            {
                acc_seq = seq++,
                acc_code = AR_RECEIVABLE_ACC,
                div_code = "PTL",
                ou_det = "00000",
                dr_amt = headerAmt,
                cr_amt = 0,
                remark = ""
            });

            decimal totalVat = 0;
            foreach (var s in subs)
            {
                var gross = s.curr_amt ?? 0;
                var vat = CalcVat(gross);
                var preVat = gross - vat;
                totalVat += vat;

                entries.Add(new ArTransactionAcc
                {
                    acc_seq = seq++,
                    acc_code = AR_REVENUE_ACC,
                    div_code = "PTL",
                    ou_det = "00000",
                    dr_amt = 0,
                    cr_amt = preVat,
                    remark = s.remark ?? ""
                });
            }

            entries.Add(new ArTransactionAcc
            {
                acc_seq = seq++,
                acc_code = AR_VAT_ACC,
                div_code = "PTL",
                ou_det = "00000",
                dr_amt = 0,
                cr_amt = totalVat,
                remark = ""
            });

            return entries;
        }

        private static decimal CalcVat(decimal grossAmount)
        {
            return Math.Round(grossAmount * VAT_RATE / (1 + VAT_RATE), 2, MidpointRounding.AwayFromZero);
        }

        // AR-DODO: ส่งจำนวนลิตร + ประเภทน้ำมันไป InsertArTransPriceList แล้วให้ ERP หาราคาเอง
        private const string PRICE_LIST_UNIT = "LTR";

        // token ใน remark (รูปแบบ "DODO-FUEL001") -> (item_type, item_code)
        private static readonly Dictionary<string, (string ItemType, string ItemCode)> FuelMap =
            new(StringComparer.OrdinalIgnoreCase)
        {
            { "FUEL001", ("Diesel",     "FG010101-001") },
            { "FUEL002", ("Benzene 91", "FG010101-002") },
            { "FUEL003", ("Benzene 95", "FG010101-003") }
        };

        private static readonly Regex FuelTokenRegex =
            new(@"FUEL\d{3}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static ArPriceListPayload MapArPriceList(TransactionAggregate t, DateTime now)
        {
            var header = t.ArTransaction;
            var subs = t.ArSubTransaction ?? new List<ArSubTransactionRecord>();
            var today = now.Date;

            var masterData = new ArPriceMasterData
            {
                ou_code = "PTL",
                customer_code = header.vendor_code,
                order_date = header.ref_doc_date ?? today,
                delivery_date = header.due_date ?? today,
                cr_by = "API",
                prog_id = "API_PROCESS"
            };

            var itemData = new List<ArPriceItemData>();
            foreach (var s in subs)
            {
                var (itemType, itemCode) = ResolveFuel(s.remark);
                if (itemType == null)
                    throw new InvalidOperationException(
                        $"AR-DODO: cannot resolve fuel token from remark '{s.remark}' (group {header.group_id}, seq {s.seq})");

                itemData.Add(new ArPriceItemData
                {
                    seq = s.seq,
                    item_type = itemType,
                    item_code = itemCode,
                    item_qty = s.curr_amt,
                    item_unit_code = PRICE_LIST_UNIT,
                    cr_by = "API",
                    prog_id = "API_PROCESS"
                });
            }

            return new ArPriceListPayload
            {
                masterData = masterData,
                itemData = itemData
            };
        }

        private static (string ItemType, string ItemCode) ResolveFuel(string remark)
        {
            if (string.IsNullOrWhiteSpace(remark))
                return (null, null);

            var match = FuelTokenRegex.Match(remark);
            if (!match.Success)
                return (null, null);

            return FuelMap.TryGetValue(match.Value, out var pair)
                ? pair
                : (null, null);
        }
    }
}
