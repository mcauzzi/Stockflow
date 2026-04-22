// Mock data for UI features not yet backed by the server.
// Each array/object mirrors the design's sim.js initial state.

export interface MockOrder {
  id: string; sku: string; lines: number; pri: 'HI' | 'MED' | 'LOW';
  progress: number; status: 'active' | 'pending' | 'done' | 'late';
  eta: number; elapsed: number;
}

export interface MockMission {
  id: string; kind: string; traslo: string;
  from: string; to: string;
  phase: 'idle' | 'move' | 'pick' | 'drop'; pct: number;
}

export interface MockEvent {
  t: string; src: string; sev: 'i' | 'w' | 'e'; msg: string;
}

export const INITIAL_ORDERS: MockOrder[] = [
  { id: 'ORD-00412', sku: 'SKU-A04', lines: 3, pri: 'HI',  progress: 0.82, status: 'active',  eta: 42,  elapsed: 210 },
  { id: 'ORD-00413', sku: 'SKU-B12', lines: 1, pri: 'MED', progress: 0.95, status: 'active',  eta: 18,  elapsed: 340 },
  { id: 'ORD-00414', sku: 'SKU-A01', lines: 5, pri: 'HI',  progress: 0.44, status: 'active',  eta: 180, elapsed: 150 },
  { id: 'ORD-00415', sku: 'SKU-C08', lines: 2, pri: 'LOW', progress: 0.22, status: 'active',  eta: 320, elapsed: 90  },
  { id: 'ORD-00416', sku: 'SKU-B03', lines: 4, pri: 'MED', progress: 0.12, status: 'pending', eta: 510, elapsed: 20  },
  { id: 'ORD-00417', sku: 'SKU-A09', lines: 1, pri: 'HI',  progress: 0.00, status: 'pending', eta: 600, elapsed: 0   },
  { id: 'ORD-00409', sku: 'SKU-A02', lines: 2, pri: 'HI',  progress: 1.00, status: 'done',    eta: 0,   elapsed: 280 },
  { id: 'ORD-00410', sku: 'SKU-C01', lines: 3, pri: 'MED', progress: 1.00, status: 'done',    eta: 0,   elapsed: 310 },
  { id: 'ORD-00411', sku: 'SKU-B07', lines: 1, pri: 'LOW', progress: 1.00, status: 'late',    eta: 0,   elapsed: 640 },
];

export const INITIAL_MISSIONS: MockMission[] = [
  { id: 'M-8842', kind: 'retrieve', traslo: 'T-01', from: 'A4·L3·C18', to: 'OUT-1',     phase: 'move', pct: 0.62 },
  { id: 'M-8843', kind: 'store',    traslo: 'T-02', from: 'IN-1',      to: 'B2·L1·C07', phase: 'pick', pct: 0.40 },
  { id: 'M-8844', kind: 'retrieve', traslo: 'T-03', from: 'C1·L4·C22', to: 'OUT-1',     phase: 'drop', pct: 0.88 },
  { id: 'M-8845', kind: 'relocate', traslo: 'T-01', from: 'A4·L3·C19', to: 'A4·L5·C04', phase: 'idle', pct: 0.00 },
];

export const INITIAL_EVENTS: MockEvent[] = [
  { t: 'T+00:00:00', src: 'SIM',  sev: 'i', msg: 'Waiting for WebSocket connection…' },
];

export const COMPONENT_LIBRARY = [
  { group: 'CONVEYORS', items: [
    { id: 'conv',   name: 'Straight',    sym: '━', cost: 100,  hotkey: '1', kind: 'conveyor_oneway', live: true  },
    { id: 'curve',  name: 'Curve 90°',   sym: '┗', cost: 120,  hotkey: '2', kind: 'conveyor_turn',   live: true  },
    { id: 'merge',  name: 'Merge',       sym: '┳', cost: 300,  hotkey: '3', kind: 'merge',           live: false },
    { id: 'divert', name: 'Diverter',    sym: '┻', cost: 400,  hotkey: '4', kind: 'diverter',        live: false },
    { id: 'accum',  name: 'Accumulator', sym: '▣', cost: 600,  hotkey: '5', kind: 'accum',           live: false },
  ]},
  { group: 'STORAGE', items: [
    { id: 'rack',   name: 'Rack Bay',    sym: '▤', cost: 800,  hotkey: '6', kind: 'rack',            live: false },
    { id: 'rack-d', name: 'Rack Double', sym: '▥', cost: 1200, hotkey: '7', kind: 'rack-d',          live: false },
    { id: 'traslo', name: 'Traslo',      sym: '◈', cost: 5000, hotkey: '8', kind: 'traslo',          live: false },
  ]},
  { group: 'STATIONS', items: [
    { id: 'bay-in',  name: 'Inbound Bay', sym: '⇥', cost: 1500, hotkey: '9', kind: 'bay-in',  live: false },
    { id: 'bay-out', name: 'Outbound',    sym: '⇤', cost: 1500, hotkey: '0', kind: 'bay-out', live: false },
    { id: 'pick',    name: 'Picking',     sym: '◉', cost: 2000, hotkey: 'Q', kind: 'pick',    live: false },
    { id: 'qc',      name: 'QC Station',  sym: '◎', cost: 1800, hotkey: 'W', kind: 'qc',      live: false },
  ]},
];

export function genSpark(mean: number, amp: number): number[] {
  const out: number[] = [];
  let v = mean;
  for (let i = 0; i < 60; i++) {
    v += (Math.random() - 0.5) * amp;
    v = Math.max(mean * 0.3, Math.min(mean * 1.5, v));
    out.push(v);
  }
  return out;
}
