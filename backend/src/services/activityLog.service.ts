import { prisma } from "../prisma";

export async function logActivity(
  userId: number | undefined,
  action: string,
  entityType: string,
  entityId?: number,
  details?: string
) {
  await prisma.activityLog.create({
    data: { userId, action, entityType, entityId, details },
  });
}
