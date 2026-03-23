import { Route, Routes } from "react-router-dom";
import HomePage from "./pages/common/HomePage";
import MainLayout from "./layouts/MainLayout";
import AdminLayout from "./layouts/AdminLayout";
import AuthAdminMiddleware from "./middlewares/AuthAdminMiddleware";
import Service from "./pages/common/Service";
import News from "./pages/common/News";
import About from "./pages/common/About";
import Login from "./pages/common/Login";
import NotFound from "./pages/common/NotFound";
import Register from "./pages/common/Register";
import VerifyOtp from "./pages/common/VerifyOtp";
import SetPassword from "./pages/common/SetPassword";
import ForgotPassword from "./pages/common/ForgotPassword";
import ResetPassword from "./pages/common/ResetPassword";
import ManageUser from "./pages/admin/ManageUser";
import ApproveStation from "./pages/admin/ApproveStation";
import AdminProfile from "./pages/admin/AdminProfile";
import EditAdminProfile from "./pages/admin/EditAdminProfile";
import ChargingStations from "./pages/ChargingStations";
import CreateStation from "./pages/CreateStation";
import EditStation from "./pages/EditStation";
import StationDetail from "./pages/StationDetail";
import OwnerPage from "./pages/owner/OwnerPage";
import OwnerLayout from "./layouts/OwnerLayout";
import CreateChargingStation from "./pages/owner/CreateChargingStation";
import PublicMiddleware from "./middlewares/PublicMiddleware";
import AuthOwnerMiddleware from "./middlewares/AuthOwnerMiddleware";
import AuthDriverMiddleware from "./middlewares/AuthDriverMiddleware";
import RegisterVerifyOtp from "./pages/common/RegisterVerifyOtp";
import RegisterCreateAccount from "./pages/common/RegisterCreateAccount";
import DriverProfile from "./pages/driver/DriverProfile";
import DriverEditProfile from "./pages/driver/EditDriverProfile";
import ScanQR from "./pages/driver/ScanQR";
import CheckInResult from "./pages/driver/CheckInResult";
import ChargingActive from "./pages/driver/ChargingActive";
import ChargingComplete from "./pages/driver/ChargingComplete";
import OwnerProfile from "./pages/owner/OwnerProfile";
import OwnerEditProfile from "./pages/owner/EditOwnerProfile";
import StationMap from "./pages/driver/StationMap";
import StationDetailDriver from "./pages/driver/StationDetailDriver";
import BookingForm from "./pages/driver/BookingForm";
import MyBookings from "./pages/driver/MyBookings";
import BookingStatus from "./pages/driver/BookingStatus";
import PaymentResult from "./pages/driver/PaymentResult";
import DriverWallet from "./pages/driver/DriverWallet";
import BookingRequests from "./pages/owner/BookingRequests";
import BookingRequestDetail from "./pages/owner/BookingRequestDetail";
import SubmitDispute from "./pages/driver/SubmitDispute";
import DisputeDetail from "./pages/driver/DisputeDetail";
import OwnerDisputeDetail from "./pages/owner/OwnerDisputeDetail";
import OwnerActiveSessions from "./pages/owner/OwnerActiveSessions";
import DisputeList from "./pages/admin/DisputeList";
import AdminDisputeDetail from "./pages/admin/AdminDisputeDetail";
import AdminRevenue from "./pages/admin/AdminRevenue";

export default function App() {
  return (
    <div>
      <Routes>
        <Route element={<MainLayout />}>
          <Route index element={<HomePage />} />
          <Route path="service" element={<Service />} />
          <Route path="news" element={<News />} />
          <Route path="about" element={<About />} />
          <Route path="driver/map" element={<StationMap />} />
          <Route path="driver/station/:id" element={<StationDetailDriver />} />
          <Route path="driver/station/:stationId/book" element={<BookingForm />} />
          <Route path="payment/result" element={<PaymentResult />} />
          <Route path="setPassword" element={<SetPassword />} />
          <Route path="forgotPassword" element={<ForgotPassword />} />
          <Route path="verifyOtp" element={<VerifyOtp />} />
          <Route path="reset-password" element={<ResetPassword />} />
          <Route element={<PublicMiddleware />}>
            <Route path="login" element={<Login />} />
            <Route path="register" element={<Register />} />
            <Route path="register/verify-otp" element={<RegisterVerifyOtp />} />
            <Route
              path="register/create-account"
              element={<RegisterCreateAccount />}
            />
          </Route>
        </Route>
        <Route element={<AuthOwnerMiddleware />}>
          <Route element={<OwnerLayout />}>
            <Route path="stations" element={<OwnerPage />} />
            <Route path="stations/add" element={<CreateChargingStation />} />
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
          </Route>
        </Route>
        <Route path="owner" element={<OwnerLayout />}>
          <Route path="owner-profile" element={<OwnerProfile />} />
          <Route path="update-owner-profile" element={<OwnerEditProfile />} />
          <Route path="booking-requests" element={<BookingRequests />} />
          <Route path="booking/:id" element={<BookingRequestDetail />} />
          <Route path="dispute/:disputeId" element={<OwnerDisputeDetail />} />
          <Route path="active-sessions" element={<OwnerActiveSessions />} />
        </Route>
        <Route path="*" element={<NotFound />} />
      </Routes>
      {/* Charging Stations Routes */}
      {/* <Route path="/stations" element={<ChargingStations />} />
          <Route path="/stations/create" element={<CreateStation />} />
          <Route path="/stations/:id" element={<StationDetail />} />
          <Route path="/stations/:id/edit" element={<EditStation />} /> */}
    </div>
  );
}
