import { writable, derived, get } from 'svelte/store';
import {
  GetMovementBasesEndpointClient,
  GetEquipmentsEndpointClient,
  GetMovementModifiersEndpointClient,
  type MovementBaseDTO,
  type EquipmentDTO,
  type MovementModifierDTO,
} from '$lib/api/ApiClient';

// ─────────────── Types ───────────────

export type WizardStep = 'input' | 'review' | 'commit' | 'done';

export interface DraftLiftSet {
  recommendedReps: number | null;
  recommendedWeight: number | null;
  recommendedRPE: number | null;
  actualReps: number;
  actualWeight: number;
  actualRPE: number;
  weightUnit: string;
}

export interface DraftDTSet {
  actualDistance: number;
  actualDuration: string; // ISO duration or seconds
  actualPace: string;
  intervalType: string;
  distanceUnit: string;
  actualRPE: number;
}

export interface DraftMovement {
  draftId: string;
  movementBaseName: string;
  equipmentName: string;
  modifierName: string | null;
  liftSets: DraftLiftSet[] | null;
  distanceTimeSets: DraftDTSet[] | null;
  notes: string;
  ordering: number;
}

export interface DraftSession {
  draftId: string;
  date: string; // ISO date string
  notes: string;
  movements: DraftMovement[];
}

export interface EntityMatch {
  entityId: string;
  name: string;
  confidence: number;
}

export interface UnresolvedReference {
  rawName: string;
  entityType: 'MovementBase' | 'Equipment' | 'MovementModifier';
  candidates: EntityMatch[];
}

export interface Resolution {
  existingId: string | null; // mapped to existing entity
  createNew: boolean; // create as new entity with rawName
}

export interface IngestionCommitResponse {
  createdSessionIDs: string[];
  createdDependencies: string[];
  errors: string[];
}

// ─────────────── Stores ───────────────

export const currentStep = writable<WizardStep>('input');
export const isLoading = writable(false);
export const errorMsg = writable('');

// Input
export const rawText = writable('');

// Parse results
export const draftSessions = writable<DraftSession[]>([]);
export const unresolvedRefs = writable<UnresolvedReference[]>([]);
export const parseWarnings = writable<string[]>([]);

// Resolution decisions: key = "EntityType:RawName"
export const resolutions = writable<Record<string, Resolution>>({});

// Existing entities for resolution dropdowns
export const existingMovementBases = writable<MovementBaseDTO[]>([]);
export const existingEquipment = writable<EquipmentDTO[]>([]);
export const existingModifiers = writable<MovementModifierDTO[]>([]);

// Commit results
export const commitResult = writable<IngestionCommitResponse | null>(null);

// ─────────────── Derived ───────────────

export const allResolved = derived(
  [unresolvedRefs, resolutions],
  ([$unresolvedRefs, $resolutions]) => {
    return $unresolvedRefs.every(ref => {
      const key = `${ref.entityType}:${ref.rawName}`;
      const resolution = $resolutions[key];
      return resolution && (resolution.existingId !== null || resolution.createNew);
    });
  }
);

export const sessionCount = derived(draftSessions, $s => $s.length);
export const movementCount = derived(draftSessions, $s =>
  $s.reduce((acc, session) => acc + session.movements.length, 0)
);
export const newEntityCount = derived(resolutions, $r =>
  Object.values($r).filter(r => r.createNew).length
);

// ─────────────── Actions ───────────────

export async function fetchExistingEntities(): Promise<void> {
  try {
    const [bases, equip, mods] = await Promise.all([
      new GetMovementBasesEndpointClient().get(),
      new GetEquipmentsEndpointClient().get(),
      new GetMovementModifiersEndpointClient().get(),
    ]);
    existingMovementBases.set(bases);
    existingEquipment.set(equip);
    existingModifiers.set(mods);
  } catch (e: any) {
    console.error('Failed to load existing entities:', e);
  }
}

export async function parseInput(): Promise<void> {
  const text = get(rawText).trim();
  if (!text) {
    errorMsg.set('Please enter workout text to import.');
    return;
  }

  isLoading.set(true);
  errorMsg.set('');

  try {
    const response = await fetch('/api/ingestion/parse', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ rawText: text }),
    });

    if (!response.ok) {
      const err = await response.json().catch(() => null);
      throw new Error(err?.detail || err?.title || `Parse failed (${response.status})`);
    }

    const data = await response.json();

    draftSessions.set(data.sessions || []);
    unresolvedRefs.set(data.unresolvedReferences || []);
    parseWarnings.set(data.warnings || []);
    resolutions.set({});

    // Auto-resolve exact matches: if no unresolved refs, go straight to review
    // Initialize resolutions for unresolved refs that have no candidates as "create new"
    const initialResolutions: Record<string, Resolution> = {};
    for (const ref of data.unresolvedReferences || []) {
      const key = `${ref.entityType}:${ref.rawName}`;
      if (ref.candidates.length === 0) {
        initialResolutions[key] = { existingId: null, createNew: true };
      }
    }
    resolutions.set(initialResolutions);

    await fetchExistingEntities();
    currentStep.set('review');
  } catch (e: any) {
    errorMsg.set(e?.message || 'Failed to parse workout text.');
  } finally {
    isLoading.set(false);
  }
}

export async function commitDraft(): Promise<void> {
  const sessions = get(draftSessions);
  const res = get(resolutions);
  const bases = get(existingMovementBases);
  const equip = get(existingEquipment);

  if (sessions.length === 0) {
    errorMsg.set('No sessions to commit.');
    return;
  }

  isLoading.set(true);
  errorMsg.set('');

  try {
    // Transform draft sessions + resolutions into commit request
    const resolvedSessions = sessions.map(session => ({
      date: session.date,
      notes: session.notes,
      trainingProgramID: null,
      movements: session.movements.map(mov => {
        const baseResolution = res[`MovementBase:${mov.movementBaseName}`];
        const equipResolution = res[`Equipment:${mov.equipmentName}`];

        // Find existing ID by exact name match if not in unresolved
        const existingBase = bases.find(b => b.name.toLowerCase() === mov.movementBaseName.toLowerCase());
        const existingEquip = equip.find(e => e.name.toLowerCase() === mov.equipmentName.toLowerCase());

        return {
          movementBaseID: baseResolution?.existingId || existingBase?.movementBaseID || null,
          newMovementBaseName: (baseResolution?.createNew || (!baseResolution?.existingId && !existingBase))
            ? mov.movementBaseName : null,
          equipmentID: equipResolution?.existingId || existingEquip?.equipmentID || null,
          newEquipmentName: (equipResolution?.createNew || (!equipResolution?.existingId && !existingEquip))
            ? mov.equipmentName : null,
          modifierName: mov.modifierName,
          liftSets: mov.liftSets,
          distanceTimeSets: mov.distanceTimeSets,
          notes: mov.notes,
          ordering: mov.ordering,
        };
      }),
    }));

    const response = await fetch('/api/ingestion/commit', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ sessions: resolvedSessions }),
    });

    if (!response.ok) {
      const err = await response.json().catch(() => null);
      throw new Error(err?.detail || err?.title || `Commit failed (${response.status})`);
    }

    const data = await response.json();
    commitResult.set(data);
    currentStep.set('done');
  } catch (e: any) {
    errorMsg.set(e?.message || 'Failed to commit sessions.');
  } finally {
    isLoading.set(false);
  }
}

export function resolveReference(entityType: string, rawName: string, resolution: Resolution): void {
  resolutions.update(r => ({
    ...r,
    [`${entityType}:${rawName}`]: resolution,
  }));
}

export function updateDraftMovement(
  sessionIdx: number,
  movementIdx: number,
  patch: Partial<DraftMovement>
): void {
  draftSessions.update(sessions => {
    const updated = [...sessions];
    const session = { ...updated[sessionIdx] };
    const movements = [...session.movements];
    movements[movementIdx] = { ...movements[movementIdx], ...patch };
    session.movements = movements;
    updated[sessionIdx] = session;
    return updated;
  });
}

export function removeDraftSession(idx: number): void {
  draftSessions.update(sessions => sessions.filter((_, i) => i !== idx));
}

export function removeDraftMovement(sessionIdx: number, movementIdx: number): void {
  draftSessions.update(sessions => {
    const updated = [...sessions];
    const session = { ...updated[sessionIdx] };
    session.movements = session.movements.filter((_, i) => i !== movementIdx);
    updated[sessionIdx] = session;
    return updated;
  });
}

export function updateDraftSession(idx: number, patch: Partial<DraftSession>): void {
  draftSessions.update(sessions => {
    const updated = [...sessions];
    updated[idx] = { ...updated[idx], ...patch };
    return updated;
  });
}

export function resetIngestion(): void {
  currentStep.set('input');
  isLoading.set(false);
  errorMsg.set('');
  rawText.set('');
  draftSessions.set([]);
  unresolvedRefs.set([]);
  parseWarnings.set([]);
  resolutions.set({});
  commitResult.set(null);
}
