import { useEffect } from "react";
import { useNavigate } from "react-router-dom";

/**
 * AdminPayouts — Đã DEPRECATED (Phase 6)
 * Hệ thống Payout cũ đã bị xóa, chuyển hướng sang AdminWithdraws
 */
export default function AdminPayouts() {
  const navigate = useNavigate();

  useEffect(() => {
    navigate("/admin/withdraws", { replace: true });
  }, [navigate]);

  return null;
}
