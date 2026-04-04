import { lazy, Suspense } from "react";
import { Route, Routes } from "react-router-dom";
import { ToastContainer } from "./components/Toast";
import { ConfirmDialogContainer } from "./components/ConfirmDialog";

// Layouts & Middlewares — load eagerly (needed on every route)
import MainLayout from "./layouts/MainLayout";
import AdminLayout from "./layouts/AdminLayout";
import OwnerLayout from "./layouts/OwnerLayout";
import AuthAdminMiddleware from "./middlewares/AuthAdminMiddleware";
import AuthOwnerMiddleware from "./middlewares/AuthOwnerMiddleware";
import AuthDriverMiddleware from "./middlewares/AuthDriverMiddleware";
import PublicMiddleware from "./middlewares/PublicMiddleware";
import AiCopilot from "./components/AiCopilot";

// ── Common pages ──────────────────────────────────────────────
const HomePage        = lazy(() => import("./pages/common/HomePage"));
const Service         = lazy(() => import("./pages/common/Service"));
const News            = lazy(() => import("./pages/common/News"));
const About           = lazy(() => import("./pages/common/About"));
const Login           = lazy(() => import("./pages/common/Login"));
const Register        = lazy(() => import("./pages/common/Register"));
const ForgotPassword  = lazy(() => import("./pages/common/ForgotPassword"));
const ChangePassword  = lazy(() => import("./pages/common/ChangePassword"));
const NotFound        = lazy(() => import("./pages/common/NotFound"));
const ChatList        = lazy(() => import("./pages/common/ChatList"));
const ChatPage        = lazy(() => import("./pages/common/ChatPage"));

// ── Admin pages ───────────────────────────────────────────────
const ManageUser        = lazy(() => import("./pages/admin/ManageUser"));
const ApproveStation    = lazy(() => import("./pages/admin/ApproveStation"));
const AdminProfile      = lazy(() => import("./pages/admin/AdminProfile"));
const EditAdminProfile  = lazy(() => import("./pages/admin/EditAdminProfile"));
const DisputeList       = lazy(() => import("./pages/admin/DisputeList"));
const AdminDisputeDetail = lazy(() => import("./pages/admin/AdminDisputeDetail"));
const AdminRevenue      = lazy(() => import("./pages/admin/AdminRevenue"));
const AdminSystemConfig = lazy(() => import("./pages/admin/AdminSystemConfig"));
const AdminPayouts      = lazy(() => import("./pages/admin/AdminPayouts"));
const AdminWithdraws    = lazy(() => import("./pages/admin/AdminWithdraws"));
const AdminDashboard    = lazy(() => import("./pages/admin/AdminDashboard"));

// ── Driver pages ──────────────────────────────────────────────
const DriverProfile       = lazy(() => import("./pages/driver/DriverProfile"));
const DriverEditProfile   = lazy(() => import("./pages/driver/EditDriverProfile"));
const ScanQR              = lazy(() => import("./pages/driver/ScanQR"));
const CheckInResult       = lazy(() => import("./pages/driver/CheckInResult"));
const ChargingActive      = lazy(() => import("./pages/driver/ChargingActive"));
const ChargingComplete    = lazy(() => import("./pages/driver/ChargingComplete"));
const StationMap          = lazy(() => import("./pages/driver/StationMap"));
const StationDetailDriver = lazy(() => import("./pages/driver/StationDetailDriver"));
const BookingForm         = lazy(() => import("./pages/driver/BookingForm"));
const MyBookings          = lazy(() => import("./pages/driver/MyBookings"));
const BookingStatus       = lazy(() => import("./pages/driver/BookingStatus"));
const DriverWallet        = lazy(() => import("./pages/driver/DriverWallet"));
const SubmitDispute       = lazy(() => import("./pages/driver/SubmitDispute"));
const DisputeDetail       = lazy(() => import("./pages/driver/DisputeDetail"));
const DriverReviews       = lazy(() => import("./pages/driver/DriverReviews"));
const FavoriteStations    = lazy(() => import("./pages/driver/FavoriteStations"));
const DriverLoyalty       = lazy(() => import("./pages/driver/DriverLoyalty"));

// ── Owner pages ───────────────────────────────────────────────
const OwnerPage             = lazy(() => import("./pages/owner/OwnerPage"));
const CreateChargingStation = lazy(() => import("./pages/owner/CreateChargingStation"));
const EditChargingStation   = lazy(() => import("./pages/owner/EditChargingStation"));
const OwnerProfile          = lazy(() => import("./pages/owner/OwnerProfile"));
const OwnerEditProfile      = lazy(() => import("./pages/owner/EditOwnerProfile"));
const BookingRequests       = lazy(() => import("./pages/owner/BookingRequests"));
const BookingRequestDetail  = lazy(() => import("./pages/owner/BookingRequestDetail"));
const OwnerDisputeDetail    = lazy(() => import("./pages/owner/OwnerDisputeDetail"));
const OwnerActiveSessions   = lazy(() => import("./pages/owner/OwnerActiveSessions"));
const OwnerWallet           = lazy(() => import("./pages/owner/OwnerWallet"));
const OwnerReviews          = lazy(() => import("./pages/owner/OwnerReviews"));
const OwnerExtraServices    = lazy(() => import("./pages/owner/OwnerExtraServices"));
const OwnerDashboard        = lazy(() => import("./pages/owner/OwnerDashboard"));

// Loading fallback
const PageLoader = () => (
  <div style={{
    minHeight: "60vh",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
  }}>
    <div style={{
      width: 40,
      height: 40,
      border: "3px solid #fed7aa",
      borderTop: "3px solid #f97316",
      borderRadius: "50%",
      animation: "spin 0.8s linear infinite",
    }} />
    <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
  </div>
);

export default function App() {
  return (
    <div>
      <Routes>
        {/* ── Public / MainLayout routes ─────────────────────────── */}
        <Route element={<MainLayout />}>
          <Route index element={<Suspense fallback={<PageLoader />}><HomePage /></Suspense>} />
          <Route path="service" element={<Suspense fallback={<PageLoader />}><Service /></Suspense>} />
          <Route path="news" element={<Suspense fallback={<PageLoader />}><News /></Suspense>} />
          <Route path="about" element={<Suspense fallback={<PageLoader />}><About /></Suspense>} />
          <Route path="driver/map" element={<Suspense fallback={<PageLoader />}><StationMap /></Suspense>} />
          <Route path="driver/station/:id" element={<Suspense fallback={<PageLoader />}><StationDetailDriver /></Suspense>} />
          <Route path="driver/station/:stationId/book" element={<Suspense fallback={<PageLoader />}><BookingForm /></Suspense>} />
          <Route path="forgotPassword" element={<Suspense fallback={<PageLoader />}><ForgotPassword /></Suspense>} />
          <Route element={<PublicMiddleware />}>
            <Route path="login" element={<Suspense fallback={<PageLoader />}><Login /></Suspense>} />
            <Route path="register" element={<Suspense fallback={<PageLoader />}><Register /></Suspense>} />
          </Route>
        </Route>

        {/* ── Owner: /stations (dùng OwnerLayout) ───────────────── */}
        <Route element={<AuthOwnerMiddleware />}>
          <Route element={<OwnerLayout />}>
            <Route path="stations" element={<Suspense fallback={<PageLoader />}><OwnerPage /></Suspense>} />
            <Route path="stations/add" element={<Suspense fallback={<PageLoader />}><CreateChargingStation /></Suspense>} />
            <Route path="stations/edit/:id" element={<Suspense fallback={<PageLoader />}><EditChargingStation /></Suspense>} />
          </Route>
        </Route>

        {/* ── Admin routes ───────────────────────────────────────── */}
        <Route element={<AuthAdminMiddleware />}>
          <Route path="admin" element={<AdminLayout />}>
            <Route path="manage-users" element={<Suspense fallback={<PageLoader />}><ManageUser /></Suspense>} />
            <Route path="approve-station" element={<Suspense fallback={<PageLoader />}><ApproveStation /></Suspense>} />
            <Route path="disputes" element={<Suspense fallback={<PageLoader />}><DisputeList /></Suspense>} />
            <Route path="disputes/:disputeId" element={<Suspense fallback={<PageLoader />}><AdminDisputeDetail /></Suspense>} />
            <Route path="admin-profile" element={<Suspense fallback={<PageLoader />}><AdminProfile /></Suspense>} />
            <Route path="edit-admin-profile" element={<Suspense fallback={<PageLoader />}><EditAdminProfile /></Suspense>} />
            <Route path="view-financial-report" element={<Suspense fallback={<PageLoader />}><AdminRevenue /></Suspense>} />
            <Route path="system-config" element={<Suspense fallback={<PageLoader />}><AdminSystemConfig /></Suspense>} />
            <Route path="payouts" element={<Suspense fallback={<PageLoader />}><AdminPayouts /></Suspense>} />
            <Route path="withdraws" element={<Suspense fallback={<PageLoader />}><AdminWithdraws /></Suspense>} />
            <Route path="change-password" element={<Suspense fallback={<PageLoader />}><ChangePassword /></Suspense>} />
            <Route path="dashboard" element={<Suspense fallback={<PageLoader />}><AdminDashboard /></Suspense>} />
          </Route>
        </Route>

        {/* ── Driver routes ──────────────────────────────────────── */}
        <Route element={<AuthDriverMiddleware />}>
          <Route path="driver" element={<MainLayout />}>
            <Route path="driver-profile" element={<Suspense fallback={<PageLoader />}><DriverProfile /></Suspense>} />
            <Route path="update-driver-profile" element={<Suspense fallback={<PageLoader />}><DriverEditProfile /></Suspense>} />
            <Route path="scan-qr" element={<Suspense fallback={<PageLoader />}><ScanQR /></Suspense>} />
            <Route path="check-in-result" element={<Suspense fallback={<PageLoader />}><CheckInResult /></Suspense>} />
            <Route path="charging" element={<Suspense fallback={<PageLoader />}><ChargingActive /></Suspense>} />
            <Route path="charging-complete" element={<Suspense fallback={<PageLoader />}><ChargingComplete /></Suspense>} />
            <Route path="my-bookings" element={<Suspense fallback={<PageLoader />}><MyBookings /></Suspense>} />
            <Route path="booking/:id" element={<Suspense fallback={<PageLoader />}><BookingStatus /></Suspense>} />
            <Route path="dispute/submit/:bookingId" element={<Suspense fallback={<PageLoader />}><SubmitDispute /></Suspense>} />
            <Route path="dispute/:disputeId" element={<Suspense fallback={<PageLoader />}><DisputeDetail /></Suspense>} />
            <Route path="wallet" element={<Suspense fallback={<PageLoader />}><DriverWallet /></Suspense>} />
            <Route path="reviews" element={<Suspense fallback={<PageLoader />}><DriverReviews /></Suspense>} />
            <Route path="favorites" element={<Suspense fallback={<PageLoader />}><FavoriteStations /></Suspense>} />
            <Route path="loyalty" element={<Suspense fallback={<PageLoader />}><DriverLoyalty /></Suspense>} />
            <Route path="chat-list" element={<Suspense fallback={<PageLoader />}><ChatList /></Suspense>} />
            <Route path="chat/:bookingId" element={<Suspense fallback={<PageLoader />}><ChatPage /></Suspense>} />
            <Route path="change-password" element={<Suspense fallback={<PageLoader />}><ChangePassword /></Suspense>} />
          </Route>
        </Route>

        {/* ── Owner routes: /owner/... ───────────────────────────── */}
        <Route element={<AuthOwnerMiddleware />}>
          <Route path="owner" element={<OwnerLayout />}>
            <Route path="owner-profile" element={<Suspense fallback={<PageLoader />}><OwnerProfile /></Suspense>} />
            <Route path="update-owner-profile" element={<Suspense fallback={<PageLoader />}><OwnerEditProfile /></Suspense>} />
            <Route path="booking-requests" element={<Suspense fallback={<PageLoader />}><BookingRequests /></Suspense>} />
            <Route path="booking/:id" element={<Suspense fallback={<PageLoader />}><BookingRequestDetail /></Suspense>} />
            <Route path="dispute/:disputeId" element={<Suspense fallback={<PageLoader />}><OwnerDisputeDetail /></Suspense>} />
            <Route path="active-sessions" element={<Suspense fallback={<PageLoader />}><OwnerActiveSessions /></Suspense>} />
            <Route path="wallet" element={<Suspense fallback={<PageLoader />}><OwnerWallet /></Suspense>} />
            <Route path="reviews" element={<Suspense fallback={<PageLoader />}><OwnerReviews /></Suspense>} />
            <Route path="extra-services" element={<Suspense fallback={<PageLoader />}><OwnerExtraServices /></Suspense>} />
            <Route path="chat-list" element={<Suspense fallback={<PageLoader />}><ChatList /></Suspense>} />
            <Route path="chat/:bookingId" element={<Suspense fallback={<PageLoader />}><ChatPage /></Suspense>} />
            <Route path="change-password" element={<Suspense fallback={<PageLoader />}><ChangePassword /></Suspense>} />
            <Route path="dashboard" element={<Suspense fallback={<PageLoader />}><OwnerDashboard /></Suspense>} />
          </Route>
        </Route>

        <Route path="*" element={<Suspense fallback={<PageLoader />}><NotFound /></Suspense>} />
      </Routes>
      <ConfirmDialogContainer />
      <ToastContainer />
      <AiCopilot />
    </div>
  );
}
