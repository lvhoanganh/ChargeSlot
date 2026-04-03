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
      <Suspense fallback={<PageLoader />}>
        <Routes>
          <Route element={<MainLayout />}>
            <Route index element={<HomePage />} />
            <Route path="service" element={<Service />} />
            <Route path="news" element={<News />} />
            <Route path="about" element={<About />} />
            <Route path="driver/map" element={<StationMap />} />
            <Route path="driver/station/:id" element={<StationDetailDriver />} />
            <Route path="driver/station/:stationId/book" element={<BookingForm />} />
            <Route path="forgotPassword" element={<ForgotPassword />} />
            <Route element={<PublicMiddleware />}>
              <Route path="login" element={<Login />} />
              <Route path="register" element={<Register />} />
            </Route>
          </Route>

          <Route element={<AuthOwnerMiddleware />}>
            <Route element={<OwnerLayout />}>
            <Route path="stations" element={<OwnerPage />} />
              <Route path="stations/add" element={<CreateChargingStation />} />
              <Route path="stations/edit/:id" element={<EditChargingStation />} />
            </Route>
          </Route>

          <Route element={<AuthAdminMiddleware />}>
            <Route path="admin" element={<AdminLayout />}>
              <Route path="manage-users" element={<ManageUser />} />
              <Route path="approve-station" element={<ApproveStation />} />
              <Route path="disputes" element={<DisputeList />} />
              <Route path="disputes/:disputeId" element={<AdminDisputeDetail />} />
              <Route path="admin-profile" element={<AdminProfile />} />
              <Route path="edit-admin-profile" element={<EditAdminProfile />} />
              <Route path="view-financial-report" element={<AdminRevenue />} />
              <Route path="system-config" element={<AdminSystemConfig />} />
              <Route path="payouts" element={<AdminPayouts />} />
              <Route path="withdraws" element={<AdminWithdraws />} />
              <Route path="change-password" element={<ChangePassword />} />
              <Route path="dashboard" element={<AdminDashboard />} />
            </Route>
          </Route>

          <Route element={<AuthDriverMiddleware />}>
            <Route path="driver" element={<MainLayout />}>
              <Route path="driver-profile" element={<DriverProfile />} />
              <Route path="update-driver-profile" element={<DriverEditProfile />} />
              <Route path="scan-qr" element={<ScanQR />} />
              <Route path="check-in-result" element={<CheckInResult />} />
              <Route path="charging" element={<ChargingActive />} />
              <Route path="charging-complete" element={<ChargingComplete />} />
              <Route path="my-bookings" element={<MyBookings />} />
              <Route path="booking/:id" element={<BookingStatus />} />
              <Route path="dispute/submit/:bookingId" element={<SubmitDispute />} />
              <Route path="dispute/:disputeId" element={<DisputeDetail />} />
              <Route path="wallet" element={<DriverWallet />} />
              <Route path="reviews" element={<DriverReviews />} />
              <Route path="favorites" element={<FavoriteStations />} />
              <Route path="loyalty" element={<DriverLoyalty />} />
              <Route path="chat-list" element={<ChatList />} />
              <Route path="chat/:bookingId" element={<ChatPage />} />
              <Route path="change-password" element={<ChangePassword />} />
            </Route>
          </Route>

          <Route element={<AuthOwnerMiddleware />}>
            <Route path="owner" element={<OwnerLayout />}>
              <Route path="owner-profile" element={<OwnerProfile />} />
              <Route path="update-owner-profile" element={<OwnerEditProfile />} />
              <Route path="booking-requests" element={<BookingRequests />} />
              <Route path="booking/:id" element={<BookingRequestDetail />} />
              <Route path="dispute/:disputeId" element={<OwnerDisputeDetail />} />
              <Route path="active-sessions" element={<OwnerActiveSessions />} />
              <Route path="wallet" element={<OwnerWallet />} />
              <Route path="reviews" element={<OwnerReviews />} />
              <Route path="extra-services" element={<OwnerExtraServices />} />
              <Route path="chat-list" element={<ChatList />} />
              <Route path="chat/:bookingId" element={<ChatPage />} />
              <Route path="change-password" element={<ChangePassword />} />
              <Route path="dashboard" element={<OwnerDashboard />} />
            </Route>
          </Route>

          <Route path="*" element={<NotFound />} />
        </Routes>
      </Suspense>
      <ConfirmDialogContainer />
      <ToastContainer />
    </div>
  );
}
