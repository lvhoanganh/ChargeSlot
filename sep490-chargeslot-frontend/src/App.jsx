import { Route, Routes } from "react-router-dom";
import HomePage from "./pages/common/HomePage";
import MainLayout from "./layouts/MainLayout";
import Service from "./pages/common/Service";
import News from "./pages/common/News";
import About from "./pages/common/About";
import Login from "./pages/common/Login";
import NotFound from "./pages/common/NotFound";
import Register from "./pages/common/Register";
import VerifyOtp from "./pages/common/VerifyOtp";
import SetPassword from "./pages/common/SetPassword";
import ForgotPassword from "./pages/common/ForgotPassword";
import Profile from "./pages/common/Profile";
import EditProfile from "./pages/common/EditProfile";
import ChangePassword from "./pages/common/ChangePassword";
import ChargingStations from "./pages/ChargingStations";
import CreateStation from "./pages/CreateStation";
import EditStation from "./pages/EditStation";
import StationDetail from "./pages/StationDetail";
import OwnerPage from "./pages/owner/OwnerPage";
import OwnerLayout from "./layouts/OwnerLayout";
import CreateChargingStation from "./pages/owner/CreateChargingStation";
import PublicMiddleware from "./middlewares/PublicMiddleware";

export default function App() {
  return (
    <div>
      <Routes>
        <Route element={<MainLayout />}>
          <Route index element={<HomePage />} />
          <Route path="service" element={<Service />} />
          <Route path="news" element={<News />} />
          <Route path="about" element={<About />} />
          <Route path="verifyOtp" element={<VerifyOtp />} />
          <Route path="setPassword" element={<SetPassword />} />
          <Route path="forgotPassword" element={<ForgotPassword />} />
          <Route path="/profile" element={<Profile />} />
          <Route path="/edit-profile" element={<EditProfile />} />
          <Route path="/change-password" element={<ChangePassword />} />
          <Route element={<PublicMiddleware />}>
            <Route path="login" element={<Login />} />
            <Route path="register" element={<Register />} />
          </Route>
        </Route>
        <Route element={<OwnerLayout />}>
          <Route path="stations" element={<OwnerPage />} />
          <Route path="stations/add" element={<CreateChargingStation />} />
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
