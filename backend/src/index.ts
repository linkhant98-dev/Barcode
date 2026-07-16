import "dotenv/config";
import express from "express";
import cors from "cors";
import { authRouter } from "./routes/auth.routes";
import { sharePurchaseRouter } from "./routes/sharePurchase.routes";
import { certificateRequestRouter } from "./routes/certificateCreation.routes";
import { certificateRouter } from "./routes/certificateManagement.routes";
import { printJobRouter } from "./routes/printing.routes";
import { reportsRouter } from "./routes/reports.routes";
import { shareholdersRouter } from "./routes/shareholders.routes";

const app = express();
app.use(cors());
app.use(express.json());

app.get("/api/health", (_req, res) => res.json({ status: "ok" }));

app.use("/api/auth", authRouter);
app.use("/api/shareholders", shareholdersRouter);
app.use("/api/share-transactions", sharePurchaseRouter);
app.use("/api/certificate-requests", certificateRequestRouter);
app.use("/api/certificates", certificateRouter);
app.use("/api/print-jobs", printJobRouter);
app.use("/api/reports", reportsRouter);

app.use((err: any, _req: express.Request, res: express.Response, _next: express.NextFunction) => {
  console.error(err);
  res.status(500).json({ error: "Internal server error" });
});

const port = Number(process.env.PORT) || 4000;
app.listen(port, () => {
  console.log(`Equity Management API listening on port ${port}`);
});
