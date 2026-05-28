/**
 * Mirror dei record C# Stockflow.Webserver.Scenarios.* (formato §6.1
 * di ARCHITECTURE_StockFlow_v0.3). I campi non ancora supportati dalla
 * simulazione sono opzionali pass-through.
 */

export interface ScenarioSummary {
  id: string;
  name: string;
  description?: string | null;
}

export interface Scenario {
  id: string;
  name: string;
  description?: string | null;
  gridSize?: GridSize | null;
  budget?: number | null;
  availableComponents?: string[] | null;
  preplacedComponents?: PreplacedComponent[] | null;
  skuCatalog?: SkuCatalog | null;
  orderProfile?: OrderProfile | null;
  objectives?: Record<string, Objective> | null;
  duration?: number | null;
}

export interface GridSize {
  width: number;
  height: number;
}

export interface PreplacedComponent {
  type: string;
  position: number[]; // [x, y]
  direction: string;
}

export interface SkuCatalog {
  count: number;
  classes: Record<string, SkuClass>;
}

export interface SkuClass {
  percentage: number;
  accessFrequency: string;
}

export interface OrderProfile {
  baseRate: number;
  peakRate: number;
  peakStartTime: number;
  peakDuration: number;
  linesPerOrder?: LinesPerOrder | null;
  deadline: number;
}

export interface LinesPerOrder {
  min: number;
  max: number;
}

export interface Objective {
  type: string;
  value?: number | null;
}
