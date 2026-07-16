import { Navigate, Route, Routes } from "react-router-dom";
import { useAuth } from "./context/AuthContext";
import { getDefaultRoute } from "./navItems";
import Layout from "./components/Layout";
import Login from "./pages/Login";
import SharePurchase from "./pages/SharePurchase";
import CertificateCreation from "./pages/CertificateCreation";
import CertificateManagement from "./pages/CertificateManagement";
import CertificatePrinting from "./pages/CertificatePrinting";
import Reports from "./pages/Reports";

function ProtectedRoutes() {
  const { user, loading } = useAuth();
  if (loading) return null;
  if (!user) return <Navigate to="/login" replace />;
  return <Layout />;
}

export default function App() {
  const { user } = useAuth();
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route element={<ProtectedRoutes />}>
        <Route index element={<Navigate to={getDefaultRoute(user?.role)} replace />} />
        <Route path="/purchase" element={<SharePurchase />} />
        <Route path="/certificate-requests" element={<CertificateCreation />} />
        <Route path="/certificates" element={<CertificateManagement />} />
        <Route path="/printing" element={<CertificatePrinting />} />
        <Route path="/reports" element={<Reports />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
