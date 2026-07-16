import { Router } from "express";
import { prisma } from "../prisma";
import { requireAuth } from "../middleware/auth";

export const reportsRouter = Router();
reportsRouter.use(requireAuth);

// Certificate register.
reportsRouter.get("/certificate-register", async (_req, res) => {
  const certificates = await prisma.certificate.findMany({
    include: { shareholder: true },
    orderBy: { issueDate: "desc" },
  });
  res.json(certificates);
});

// Shareholder list with aggregated share totals across issued certificates.
reportsRouter.get("/shareholders", async (_req, res) => {
  const shareholders = await prisma.shareholder.findMany({
    include: { certificates: { where: { status: "ISSUED" } } },
    orderBy: { fullName: "asc" },
  });
  const result = shareholders.map((s) => ({
    id: s.id,
    fullName: s.fullName,
    nationalId: s.nationalId,
    email: s.email,
    phone: s.phone,
    totalIssuedCertificates: s.certificates.length,
    totalShares: s.certificates.reduce((sum, c) => sum + c.shareQuantity, 0),
  }));
  res.json(result);
});

// Print history.
reportsRouter.get("/print-history", async (_req, res) => {
  const jobs = await prisma.printJob.findMany({
    where: { status: "PRINTED" },
    include: {
      certificate: { include: { shareholder: true } },
      printedBy: { select: { id: true, name: true } },
    },
    orderBy: { printedAt: "desc" },
  });
  res.json(jobs);
});

// Activity / audit log.
reportsRouter.get("/activity-log", async (req, res) => {
  const limit = Math.min(Number(req.query.limit) || 100, 500);
  const logs = await prisma.activityLog.findMany({
    include: { user: { select: { id: true, name: true, role: true } } },
    orderBy: { createdAt: "desc" },
    take: limit,
  });
  res.json(logs);
});
