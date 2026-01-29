import type { MovementDTO, TrainingSessionDTO, MovementBaseDTO, EquipmentDTO } from '$lib/api/ApiClient';
import { TrainingSessionStatus } from '$lib/api/ApiClient';
import { dateToUS } from './formatters';

// ---------- Movement ID/Data Extraction ----------

export function idOfMovement(m: MovementDTO): string {
  return String((m as any)?.movementID ?? (m as any)?.movementId ?? '');
}

export function movementData(m: MovementDTO): any {
  return (m as any)?.movementData ?? (m as any)?.movementDataDTO ?? null;
}

export function movementBaseName(m: MovementDTO): string {
  return String(movementData(m)?.movementBase?.name ?? 'Movement');
}

export function movementEquipmentName(m: MovementDTO): string {
  return String(movementData(m)?.equipment?.name ?? '—');
}

export function movementModifierName(m: MovementDTO): string {
  return String(movementData(m)?.movementModifier?.name ?? '—');
}

export function movementNotes(m: MovementDTO): string {
  return String((m as any)?.notes ?? '');
}

export function movementCompleted(m: MovementDTO): boolean {
  return Boolean((m as any)?.isCompleted ?? false);
}

export function liftSets(m: MovementDTO): any[] {
  return ((m as any)?.liftSets ?? []) as any[];
}

export function dtSets(m: MovementDTO): any[] {
  return ((m as any)?.distanceTimeSets ?? (m as any)?.dtSets ?? []) as any[];
}

export function movementSetCount(m: MovementDTO): number {
  return liftSets(m).length + dtSets(m).length;
}

// ---------- Set ID Extraction ----------

export function setId(s: any): string {
  return String(s?.setEntryID ?? s?.setEntryId ?? '');
}

// ---------- Session Data Extraction ----------

export function sessionStatusValue(
  s: TrainingSessionDTO | null
): TrainingSessionStatus {
  if (!s) return TrainingSessionStatus._0;

  const v: any = (s as any).status;

  if (v === TrainingSessionStatus._0) return TrainingSessionStatus._0;
  if (v === TrainingSessionStatus._1) return TrainingSessionStatus._1;
  if (v === TrainingSessionStatus._2) return TrainingSessionStatus._2;
  if (v === TrainingSessionStatus._3) return TrainingSessionStatus._3;
  if (v === TrainingSessionStatus._4) return TrainingSessionStatus._4;

  const n = Number(v);
  if (n === 0) return TrainingSessionStatus._0;
  if (n === 1) return TrainingSessionStatus._1;
  if (n === 2) return TrainingSessionStatus._2;
  if (n === 3) return TrainingSessionStatus._3;
  if (n === 4) return TrainingSessionStatus._4;

  return TrainingSessionStatus._0;
}

export function sessionDateValue(s: TrainingSessionDTO | null): any {
  return s ? (s as any).date : null;
}

export function sessionDateUS(s: TrainingSessionDTO | null): string {
  return dateToUS(sessionDateValue(s));
}

export function sessionNotesValue(s: TrainingSessionDTO | null): string {
  if (!s) return '';
  return String((s as any).notes ?? '');
}

export function hasSessionNotes(s: TrainingSessionDTO | null): boolean {
  return sessionNotesValue(s).trim().length > 0;
}

// ---------- Movement Base/Equipment Extraction ----------

export function mbId(mb: MovementBaseDTO | any): string {
  return String(mb?.movementBaseID ?? mb?.movementBaseId ?? '');
}

export function mbName(mb: MovementBaseDTO | any): string {
  return String(mb?.name ?? '');
}

export function equipmentId(eq: EquipmentDTO | any): string {
  return String(eq?.equipmentID ?? eq?.equipmentId ?? '');
}

export function equipmentName(eq: EquipmentDTO | any): string {
  return String(eq?.name ?? '');
}

export function equipmentLabel(eq: EquipmentDTO | any): string {
  if (!eq) return '';
  const name = equipmentName(eq);
  const enabled = Boolean(eq?.enabled ?? true);
  return enabled ? name : `${name} (disabled)`;
}

// ---------- Set Kind Detection ----------

export type SetKind = 'none' | 'lift' | 'dt';

export function setKindFor(m: MovementDTO): SetKind {
  const lift = ((m as any)?.liftSets?.length ?? 0) > 0;
  const dt = ((m as any)?.distanceTimeSets?.length ?? 0) > 0;
  if (lift) return 'lift';
  if (dt) return 'dt';
  return 'none';
}
