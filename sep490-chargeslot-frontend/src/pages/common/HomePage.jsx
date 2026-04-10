import Nav from "@/components/Nav";
import { Button } from "@/components/ui/button";
import { useNavigate } from "react-router-dom";

export default function HomePage() {
  const navigate = useNavigate();
  return (
    <div className="pt-20">
      <div className="min-h-60 flex flex-col items-center justify-center gap-5">
        <h1 className="text-3xl font-bold">Đặt lịch sạc xe điện dễ dàng</h1>
        <p className="text-xl">
          Dễ dàng đặt trước lịch sạc cho xe điện của bạn với ChargeSlot. Trải
          nghiệm sự tiện lợi và tiết kiệm thời gian
        </p>
        <Button
          className="w-40 h-11 bg-orange-500 rounded-full text-xl hover:bg-green-500 cursor-pointer"
          onClick={() => navigate("/driver/map")}
        >
          Tìm trạm sạc
        </Button>
      </div>
      <img
        src="/home.jpg"
        alt="home bg"
        className="w-full h-[800px] object-cover"
      />
    </div>
  );
}
