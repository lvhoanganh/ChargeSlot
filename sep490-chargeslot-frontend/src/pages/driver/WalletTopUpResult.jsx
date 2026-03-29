import { useSearchParams, Link } from "react-router-dom";
import { CheckCircle, XCircle } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useEffect } from "react";
import { useAuthStore } from "@/stores/authStore";

export default function WalletTopUpResult() {
  const [searchParams] = useSearchParams();
  const success = searchParams.get("success") === "True";
  const { role } = useAuthStore(); // So we know where to redirect back to (driver vs owner wallet if they have separate wallets, but currently we mostly have DriverWallet or OwnerWallet based on role and routing). 
  
  // Nạp tiền wallet thường ở màn hình wallet của role tương ứng
  const walletPath = role === "Owner" ? "/owner/wallet" : "/driver/wallet";

  useEffect(() => {
    // Scroll to top when loaded
    window.scrollTo(0, 0);
  }, []);

  return (
    <div className="flex flex-col items-center justify-center min-h-[70vh] p-4 text-center">
      {success ? (
        <div className="animate-in fade-in zoom-in duration-500">
          <CheckCircle className="w-24 h-24 text-green-500 mx-auto mb-6" />
          <h1 className="text-3xl font-bold text-slate-800 dark:text-slate-100 mb-4">
            Nạp Tiền Thành Công!
          </h1>
          <p className="text-slate-600 dark:text-slate-300 text-lg max-w-md mx-auto mb-8">
            Giao dịch nạp tiền qua VNPay đã được xử lý thành công. Số dư ví của bạn đã được cập nhật.
          </p>
        </div>
      ) : (
        <div className="animate-in fade-in zoom-in duration-500">
          <XCircle className="w-24 h-24 text-red-500 mx-auto mb-6" />
          <h1 className="text-3xl font-bold text-slate-800 dark:text-slate-100 mb-4">
            Nạp Tiền Thất Bại
          </h1>
          <p className="text-slate-600 dark:text-slate-300 text-lg max-w-md mx-auto mb-8">
            Giao dịch nạp tiền không thành công hoặc đã bị hủy. Vui lòng thử lại.
          </p>
        </div>
      )}

      <div className="flex gap-4 mt-4">
        <Link to={walletPath}>
          <Button className="cs-btn-primary min-w-[150px]">
            Quay lại Ví
          </Button>
        </Link>
        {!success && (
          <Link to="/">
            <Button variant="outline" className="min-w-[150px]">
              Về Trang Chủ
            </Button>
          </Link>
        )}
      </div>
    </div>
  );
}
