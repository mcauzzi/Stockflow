export type Direction = 'North' | 'East' | 'South' | 'West';
export type EntityStatus = 'Idle' | 'Moving' | 'Queued';
export type SimSpeed = 0 | 1 | 2 | 3 | 4 | 5;

export const SIM_SPEEDS: { id: SimSpeed; label: string; scale: number }[] = [
  { id: 0, label: '❚❚', scale: 0 },
  { id: 1, label: '1×', scale: 1 },
  { id: 2, label: '2×', scale: 2 },
  { id: 3, label: '5×', scale: 5 },
  { id: 4, label: '10×', scale: 10 },
  { id: 5, label: 'LIVE', scale: 1 },
];

export interface Vector3 { x: number; y: number; z: number; }

export interface EntityState {
  id: number;
  sku: string;
  position: Vector3;
  status: EntityStatus;
}

export interface ComponentState {
  id: number;
  kind: string;
  gridX: number;
  gridY: number;
  facing: Direction;
}

export interface SimEvent {
  eventType: string;
  simTime: number;
  payloadJson: string;
}

export interface MetricsSnapshot {
  throughput: number;
  avgFulfillmentTime: number;
  warehouseSaturation: number;
  activeOrders: number;
  completedOrders: number;
}

export interface StateDeltaMessage {
  type: 'StateDelta';
  serverTime: number;
  simulationTime: number;
  timeScale: number;
  updatedEntities: EntityState[];
  createdEntities: EntityState[];
  removedEntityIds: number[];
  updatedComponents: ComponentState[];
  createdComponents: ComponentState[];
  removedComponentIds: number[];
  events: SimEvent[];
  metrics: MetricsSnapshot | null;
}

export interface FullStateMessage {
  type: 'FullState';
  serverTime: number;
  simulationTime: number;
  timeScale: number;
  entities: EntityState[];
  components: ComponentState[];
  metrics: MetricsSnapshot | null;
}

export interface CommandResultMessage {
  type: 'CommandResult';
  serverTime: number;
  commandId: number;
  success: boolean;
  errorMessage: string | null;
}

export type ServerMessage = StateDeltaMessage | FullStateMessage | CommandResultMessage;
