export type SessionStatus = 'Running' | 'Terminated';

export interface SessionInfo {
  id: string;
  status: SessionStatus;
  startedAt: string;     // ISO timestamp
  endedAt?: string | null;
  scenarioId?: string | null;
  simulationTime: number;
}
