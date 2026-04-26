import { Button } from "@/components/ui/button";
import { useNavigate } from "react-router-dom";

export default function SetPassword() {
  const navigate = useNavigate();

  return (
    <div className="min-h-screen bg-[#f3f4f5] flex justify-center items-center">
      <form
        className="max-w-[500px] w-full bg-white rounded-md shadow-md"
        onSubmit={(e) => {
          e.preventDefault();
          navigate("/login");
        }}
      >
        <div className="p-8">
          <h1 className="text-xl font-bold mb-5">Thiết lập mật khẩu</h1>

          <input className="h-10 w-full mb-4 border px-4" placeholder="Mật khẩu" />
          <input className="h-10 w-full mb-5 border px-4" placeholder="Xác nhận mật khẩu" />

          <Button className="w-full h-12 bg-orange-500">
            Hoàn tất
          </Button>
        </div>
      </form>
    </div>
  );
}
