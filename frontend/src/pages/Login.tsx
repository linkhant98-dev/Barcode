import { FormEvent, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { apiErrorMessage } from "../api/client";
import { getDefaultRoute } from "../navItems";

export default function Login() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState("equity.officer@cbbank.test");
  const [password, setPassword] = useState("password123");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const loggedInUser = await login(email, password);
      navigate(getDefaultRoute(loggedInUser.role));
    } catch (err) {
      setError(apiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="login-shell">
      <div className="login-card">
        <h1>Equity Management System</h1>
        <p>CB Bank PCL — Equity &amp; Shareholder Certificate Management</p>
        {error && <div className="error-banner">{error}</div>}
        <form onSubmit={handleSubmit}>
          <label>Email</label>
          <input value={email} onChange={(e) => setEmail(e.target.value)} type="email" required />
          <label>Password</label>
          <input value={password} onChange={(e) => setPassword(e.target.value)} type="password" required />
          <div className="form-actions">
            <button type="submit" disabled={loading}>
              {loading ? "Signing in..." : "Sign in"}
            </button>
          </div>
        </form>
        <div className="demo-accounts">
          <strong>Demo accounts</strong> (password: password123)
          <br />
          equity.officer@cbbank.test — Equity Officer
          <br />
          approver@cbbank.test — Approver (Manager)
          <br />
          admin@cbbank.test — System Administrator
          <br />
          inquiry@cbbank.test — Inquiry User
        </div>
      </div>
    </div>
  );
}
