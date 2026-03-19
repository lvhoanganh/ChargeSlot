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
import OwnerProfile from "./pages/owner/OwnerProfile";
import OwnerEditProfile from "./pages/owner/EditOwnerProfile";

export default function App() {
  return (
    <div>
      <Routes>
        <Route element={<MainLayout />}>
          <Route index element={<HomePage />} />
          <Route path="service" element={<Service />} />
          <Route path="news" element={<News />} />
          <Route path="about" element={<About />} />
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
            <Route path="admin-profile" element={<AdminProfile />} />
            <Route path="edit-admin-profile" element={<EditAdminProfile />} />
          </Route>
        </Route>
        <Route element={<AuthDriverMiddleware />}>
          <Route path="driver" element={<MainLayout />}>
            <Route path="driver-profile" element={<DriverProfile />} />
            <Route path="update-driver-profile" element={<DriverEditProfile />} />
          </Route>
        </Route>
        <Route path="owner" element={<OwnerLayout />}>
          <Route path="owner-profile" element={<OwnerProfile />} />
          <Route path="update-owner-profile" element={<OwnerEditProfile />} />
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
