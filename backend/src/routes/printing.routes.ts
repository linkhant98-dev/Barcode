import { Router } from "express";
import { z } from "zod";
import { prisma } from "../prisma";
import { requireAuth, requireRole } from "../middleware/auth";
import { logActivity } from "../services/activityLog.service";
import { generateBarcodePng } from "../services/barcode.service";

export const printJobRouter = Router();
printJobRouter.use(requireAuth);

const createSchema = z.object({ certificateId: z.number().int() });

// Equity Officer requests certificate printing.
printJobRouter.post("/", requireRole("EQUITY_OFFICER", "SYSTEM_ADMIN"), async (req, res) => {
  const parsed = createSchema.safeParse(req.body);
  if (!parsed.success) return res.status(400).json({ error: parsed.error.flatten() });

  const certificate = await prisma.certificate.findUnique({ where: { id: parsed.data.certificateId } });
  if (!certificate) return res.status(404).json({ error: "Certificate not found" });
  if (certificate.status !== "ISSUED") {
    return res.status(409).json({ error: "Only issued certificates can be printed" });
  }

  const printJob = await prisma.printJob.create({
    data: { certificateId: certificate.id, requestedById: req.user!.userId },
    include: { certificate: { include: { shareholder: true } }, requestedBy: { select: { id: true, name: true } } },
  });

  await logActivity(req.user!.userId, "REQUEST_PRINT", "PrintJob", printJob.id);
  res.status(201).json(printJob);
});

printJobRouter.get("/", async (req, res) => {
  const jobs = await prisma.printJob.findMany({
    include: {
      certificate: { include: { shareholder: true } },
      requestedBy: { select: { id: true, name: true } },
      approvedBy: { select: { id: true, name: true } },
      printedBy: { select: { id: true, name: true } },
    },
    orderBy: { createdAt: "desc" },
  });
  res.json(jobs);
});

// Approver (Manager) reviews and approves/rejects the printing request.
printJobRouter.post("/:id/decision", requireRole("APPROVER", "SYSTEM_ADMIN"), async (req, res) => {
  const id = Number(req.params.id);
  const approve = req.body.approve !== false;

  const job = await prisma.printJob.findUnique({ where: { id } });
  if (!job) return res.status(404).json({ error: "Print job not found" });
  if (job.status !== "REQUESTED") {
    return res.status(409).json({ error: "Print job already decided" });
  }

  const updated = await prisma.printJob.update({
    where: { id },
    data: {
      status: approve ? "APPROVED" : "REJECTED",
      approvedById: req.user!.userId,
      rejectionNote: approve ? null : req.body.note ?? null,
    },
  });

  await logActivity(req.user!.userId, approve ? "APPROVE_PRINT" : "REJECT_PRINT", "PrintJob", id);
  res.json(updated);
});

// Generate certificate (preview) — renders the certificate + barcode without marking it printed.
printJobRouter.get("/:id/preview", async (req, res) => {
  const id = Number(req.params.id);
  const job = await prisma.printJob.findUnique({
    where: { id },
    include: { certificate: { include: { shareholder: true } } },
  });
  if (!job) return res.status(404).json({ error: "Print job not found" });
  if (job.status !== "APPROVED" && job.status !== "PRINTED") {
    return res.status(409).json({ error: "Print job must be approved before preview" });
  }

  const barcodeDataUrl = await generateBarcodePng(job.certificate.barcodeValue);
  res.json({
    certificateNo: job.certificate.certificateNo,
    shareholderName: job.certificate.shareholder.fullName,
    shareQuantity: job.certificate.shareQuantity,
    issueDate: job.certificate.issueDate,
    barcodeDataUrl,
  });
});

// Print certificate — records who printed it and when (this IS the print history entry).
printJobRouter.post("/:id/print", requireRole("EQUITY_OFFICER", "SYSTEM_ADMIN"), async (req, res) => {
  const id = Number(req.params.id);
  const job = await prisma.printJob.findUnique({ where: { id } });
  if (!job) return res.status(404).json({ error: "Print job not found" });
  if (job.status !== "APPROVED") {
    return res.status(409).json({ error: "Print job must be approved before printing" });
  }

  const updated = await prisma.printJob.update({
    where: { id },
    data: { status: "PRINTED", printedAt: new Date(), printedById: req.user!.userId },
    include: { certificate: { include: { shareholder: true } } },
  });

  await logActivity(req.user!.userId, "PRINT_CERTIFICATE", "PrintJob", id);
  res.json(updated);
});
