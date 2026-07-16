import { Router } from "express";
import { prisma } from "../prisma";
import { requireAuth } from "../middleware/auth";

export const shareholdersRouter = Router();
shareholdersRouter.use(requireAuth);

shareholdersRouter.get("/", async (req, res) => {
  const q = (req.query.q as string) || "";
  const shareholders = await prisma.shareholder.findMany({
    where: q ? { fullName: { contains: q } } : undefined,
    orderBy: { fullName: "asc" },
  });
  res.json(shareholders);
});
