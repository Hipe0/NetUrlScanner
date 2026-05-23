namespace NetURLScanner.Services
{
    public static class TrustedBrandDefaults
    {
        public static Dictionary<string, string> GetDefaultBrands()
        {
            return new Dictionary<string, string>
            {
                // Quốc tế / công nghệ phổ biến
                { "google", "google.com" },
                { "facebook", "facebook.com" },
                { "microsoft", "microsoft.com" },
                { "apple", "apple.com" },
                { "github", "github.com" },
                { "paypal", "paypal.com" },
                { "tiktok", "tiktok.com" },
                { "netflix", "netflix.com" },
                { "instagram", "instagram.com" },
                { "amazon", "amazon.com" },
                { "steam", "steampowered.com" },
                { "youtube", "youtube.com" },
                { "gmail", "gmail.com" },
                { "telegram", "telegram.org" },
                { "linkedin", "linkedin.com" },
                { "zalo", "zalo.me" },

                // Thương mại điện tử
                { "shopee", "shopee.vn" },
                { "lazada", "lazada.vn" },
                { "tiki", "tiki.vn" },
                { "sendo", "sendo.vn" },

                // Ví điện tử / cổng thanh toán
                { "momo", "momo.vn" },
                { "zalopay", "zalopay.vn" },
                { "vnpay", "vnpay.vn" },
                { "vnpayapp", "vnpayapp.vn" },
                { "viettelmoney", "viettelmoney.vn" },

                // Ngân hàng Việt Nam
                { "vietcombank", "vietcombank.com.vn" },
                { "vcb", "vietcombank.com.vn" },
                { "bidv", "bidv.com.vn" },
                { "vietinbank", "vietinbank.vn" },
                { "agribank", "agribank.com.vn" },
                { "techcombank", "techcombank.com" },
                { "tcb", "techcombank.com" },
                { "mbbank", "mbbank.com.vn" },
                { "mb", "mbbank.com.vn" },
                { "acb", "acb.com.vn" },
                { "sacombank", "sacombank.com.vn" },
                { "vpbank", "vpbank.com.vn" },
                { "tpbank", "tpbank.vn" },
                { "vib", "vib.com.vn" },
                { "hdbank", "hdbank.com.vn" },
                { "ocb", "ocb.com.vn" },
                { "msb", "msb.com.vn" },
                { "seabank", "seabank.com.vn" },
                { "shb", "shb.com.vn" },
                { "eximbank", "eximbank.com.vn" },
                { "namabank", "namabank.com.vn" },
                { "lpbank", "lpbank.com.vn" },
                { "bvbank", "bvbank.net.vn" },
                { "ncb", "ncb-bank.vn" },
                { "kienlongbank", "kienlongbank.com" },
                { "vietbank", "vietbank.com.vn" },
                { "pvcombank", "pvcombank.com.vn" },
                { "saigonbank", "saigonbank.com.vn" },
                { "bacabank", "baca-bank.vn" },

                // Chứng khoán / tài chính giao dịch
                { "ssi", "ssi.com.vn" },
                { "vndirect", "vndirect.com.vn" },
                { "vps", "vps.com.vn" },
                { "tcbs", "tcbs.com.vn" },
                { "hsc", "hsc.com.vn" },
                { "mas", "masvn.com" },
                { "miraeasset", "masvn.com" },
                { "vcbs", "vcbs.com.vn" },
                { "mbs", "mbs.com.vn" },
                { "dnse", "dnse.com.vn" },
                { "fpts", "fpts.com.vn" },

                // Dịch vụ công / cơ quan nhà nước
                { "dichvucong", "dichvucong.gov.vn" },
                { "gdt", "gdt.gov.vn" },
                { "thuedientu", "thuedientu.gdt.gov.vn" },
                { "baohiemxahoi", "baohiemxahoi.gov.vn" },
                { "vssid", "baohiemxahoi.gov.vn" },
                { "vneid", "vneid.gov.vn" },
                { "sbv", "sbv.gov.vn" },

                // Vận chuyển / giao hàng
                { "viettelpost", "viettelpost.com.vn" },
                { "ghn", "ghn.vn" },
                { "ghtk", "giaohangtietkiem.vn" },
                { "jtexpress", "jtexpress.vn" },
                { "ahamove", "ahamove.com" },
                { "grab", "grab.com" },

                // Viễn thông / bán lẻ công nghệ
                { "viettel", "vietteltelecom.vn" },
                { "vinaphone", "vnpt.com.vn" },
                { "mobifone", "mobifone.vn" },
                { "fpt", "fpt.vn" },
                { "fptshop", "fptshop.com.vn" },
                { "thegioididong", "thegioididong.com" },
                { "dienmayxanh", "dienmayxanh.com" }
            };
        }
    }
}