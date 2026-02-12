// Mount state matching backend MountState enum
export type MountState = 'Healthy' | 'Degraded' | 'Stale' | 'Recovering';

// Mount health status matching backend MountHealthStatus record
export interface MountHealthStatus {
  state: MountState;
  mountPath: string;
  lastHealthyAt: string | null;
  lastCheckAt: string;
  consecutiveFailures: number;
  message: string | null;
  affectedPods: string[];
}
