import { writable, get } from 'svelte/store';
import {
  GetTrainingSessionEndpointClient,
  UpdateTrainingSessionEndpointClient,
  UpdateTrainingSessionRequest,
  DeleteTrainingSessionEndpointClient,
  DeleteMovementEndpointClient,
  UpdateMovementOrderEndpointClient,
  UpdateMovementOrderRequest,
  UpdateMovementEndpointClient,
  UpdateMovementRequest,
  CreateMovementEndpointClient,
  CreateMovementRequest,
  CreateLiftSetEntryEndpointClient,
  CreateLiftSetEntryRequest,
  UpdateLiftSetEntryEndpointClient,
  UpdateLiftSetEntryRequest,
  DeleteLiftSetEntryEndpointClient,
  CreateDTSetEntryEndpointClient,
  CreateDTSetEntryRequest,
  UpdateDTSetEntryEndpointClient,
  UpdateDTSetEntryRequest,
  DeleteDTSetEntryEndpointClient,
  GetMovementBasesEndpointClient,
  GetEquipmentsEndpointClient,
  GetMovementModifiersEndpointClient,
  TrainingSessionStatus,
  WeightUnit,
  type TrainingSessionDTO,
  type MovementDTO,
  type MovementBaseDTO,
  type EquipmentDTO,
  type MovementModifierDTO,
} from '$lib/api/ApiClient';
import { idOfMovement, setId, toIsoDateOnly } from '$lib/utils/training';

// ---------- Types ----------

export type DtFieldKey =
  | 'recommendedDistance'
  | 'actualDistance'
  | 'intervalDuration'
  | 'targetPace'
  | 'actualPace'
  | 'recommendedDuration'
  | 'actualDuration'
  | 'recommendedRest'
  | 'actualRest'
  | 'intervalType'
  | 'distanceUnit'
  | 'actualRPE';

export const DT_FIELD_LABELS: Record<DtFieldKey, string> = {
  recommendedDistance: 'Rec Dist',
  actualDistance: 'Act Dist',
  intervalDuration: 'Interval',
  targetPace: 'Target Pace',
  actualPace: 'Actual Pace',
  recommendedDuration: 'Rec Dur',
  actualDuration: 'Act Dur',
  recommendedRest: 'Rec Rest',
  actualRest: 'Act Rest',
  intervalType: 'Type',
  distanceUnit: 'Unit',
  actualRPE: 'RPE',
};

export const SESSION_STATUSES = [
  TrainingSessionStatus._0,
  TrainingSessionStatus._1,
  TrainingSessionStatus._2,
  TrainingSessionStatus._3,
];

// ---------- Stores ----------

export const session = writable<TrainingSessionDTO | null>(null);
export const movements = writable<MovementDTO[]>([]);
export const movementBases = writable<MovementBaseDTO[]>([]);
export const movementModifiers = writable<MovementModifierDTO[]>([]);
export const equipments = writable<EquipmentDTO[]>([]);
export const isLoading = writable(false);
export const errorMsg = writable('');
export const isEditing = writable(false);
export const displayWeightUnit = writable<WeightUnit>(WeightUnit._0);

// Draft state for editing
export const draftDate = writable('');
export const draftStatus = writable<TrainingSessionStatus>(TrainingSessionStatus._0);
export const draftNotes = writable('');
export const pendingOrderIds = writable<string[] | null>(null);

// Drag state
export const dragFromId = writable<string | null>(null);
export const dragOverId = writable<string | null>(null);

// Add movement draft
export const newMovementBaseId = writable('');
export const newEquipmentId = writable('');
export const newMovementModifier = writable('');
export const newMovementNotes = writable('');
export const newModifierText = writable('');

// DT field visibility per movement
export const dtFieldVisibility = writable<Record<string, Record<DtFieldKey, boolean>>>({});
export const dtFieldPickerOpen = writable<Record<string, boolean>>({});

// ---------- Actions ----------

export async function loadSession(sessionId: string, programId?: string): Promise<void> {
  isLoading.set(true);
  errorMsg.set('');
  session.set(null);
  isEditing.set(false);
  pendingOrderIds.set(null);

  try {
    const sessionClient = new GetTrainingSessionEndpointClient();
    const basesClient = new GetMovementBasesEndpointClient();
    const equipClient = new GetEquipmentsEndpointClient();
    const modifiersClient = new GetMovementModifiersEndpointClient();

    const [s, bases, eqs, mods] = await Promise.all([
      sessionClient.get(sessionId),
      basesClient.get(),
      equipClient.get(),
      modifiersClient.get(),
    ]);

    session.set(s);
    const movs = (s.movements ?? []) as MovementDTO[];
    movements.set(movs);

    // Initialize DT field visibility for each movement
    const visibility: Record<string, Record<DtFieldKey, boolean>> = {};
    const pickerOpen: Record<string, boolean> = {};

    for (const m of movs) {
      const id = idOfMovement(m);
      if (!id) continue;

      visibility[id] = {
        actualDistance: true,
        intervalDuration: true,
        actualPace: true,
        actualDuration: true,
        actualRest: true,
        actualRPE: true,
        recommendedDistance: false,
        targetPace: false,
        recommendedDuration: false,
        recommendedRest: false,
        intervalType: false,
        distanceUnit: false,
      };
      pickerOpen[id] = false;
    }

    dtFieldVisibility.set(visibility);
    dtFieldPickerOpen.set(pickerOpen);
    movementBases.set((bases ?? []) as MovementBaseDTO[]);
    movementModifiers.set((mods ?? []) as MovementModifierDTO[]);
    equipments.set((eqs ?? []) as EquipmentDTO[]);

    // Set default add-movement draft
    const firstBase = (bases ?? [])[0] as any;
    const firstEquip = (eqs ?? [])[0] as any;
    newMovementBaseId.set(firstBase?.movementBaseID ? String(firstBase.movementBaseID) : '');
    newEquipmentId.set(firstEquip?.equipmentID ? String(firstEquip.equipmentID) : '');
    newMovementNotes.set('');
    newModifierText.set('');

    // Seed session drafts
    draftDate.set(toIsoDateOnly((s as any).date));
    draftStatus.set((s as any).status ?? TrainingSessionStatus._0);
    draftNotes.set(String((s as any).notes ?? ''));
  } catch (e: any) {
    errorMsg.set(
      e?.body?.title || e?.body?.detail || e?.message || 'Failed to load session.'
    );
  } finally {
    isLoading.set(false);
  }
}

export function enterEditMode(): void {
  const s = get(session);
  if (!s) return;

  isEditing.set(true);
  draftDate.set(toIsoDateOnly((s as any).date));
  draftStatus.set((s as any).status ?? TrainingSessionStatus._0);
  draftNotes.set(String((s as any).notes ?? ''));
  movements.set((s.movements ?? []) as MovementDTO[]);
  pendingOrderIds.set(null);
}

export function cancelEditMode(sessionId: string, programId?: string): void {
  loadSession(sessionId, programId);
}

export async function saveEdits(sessionId: string): Promise<boolean> {
  const s = get(session);
  if (!s) return false;

  isLoading.set(true);
  errorMsg.set('');

  try {
    // Persist movement order if changed
    const orderIds = get(pendingOrderIds);
    if (orderIds && orderIds.length > 0) {
      const orderClient = new UpdateMovementOrderEndpointClient();
      const orderReq: UpdateMovementOrderRequest = {
        trainingSessionID: (s as any).trainingSessionID ?? sessionId,
        movements: orderIds.map((id, idx) => ({
          movementID: id,
          ordering: idx,
        })),
      } as any;
      await orderClient.put(orderReq as any);
    }

    isEditing.set(false);
    pendingOrderIds.set(null);
    return true;
  } catch (e: any) {
    errorMsg.set(
      e?.body?.title || e?.body?.detail || e?.message || 'Failed to save changes.'
    );
    return false;
  } finally {
    isLoading.set(false);
  }
}

// ---------- Session Delete ----------

export async function updateSessionField(
  sessionId: string,
  field: 'date' | 'status' | 'notes',
  value: any,
  programId?: string
): Promise<boolean> {
  const s = get(session);
  if (!s) return false;

  isLoading.set(true);
  errorMsg.set('');

  try {
    const sessionClient = new UpdateTrainingSessionEndpointClient();

    const currentDate = toIsoDateOnly((s as any).date);
    const currentStatus = (s as any).status ?? TrainingSessionStatus._0;
    const currentNotes = (s as any).notes ?? '';

    const req: UpdateTrainingSessionRequest = {
      trainingSessionID: (s as any).trainingSessionID ?? sessionId,
      trainingProgramID: (s as any).trainingProgramID ?? programId ?? null,
      date: field === 'date' ? new Date(`${value}T12:00:00`) : new Date(`${currentDate}T12:00:00`),
      status: field === 'status' ? value : currentStatus,
      notes: field === 'notes' ? value : currentNotes,
      perceivedEffortRatings: (s as any).perceivedEffortRatings ?? null,
    } as any;

    const updated = await sessionClient.put(req);
    session.set(updated);

    // Update draft values to match
    draftDate.set(toIsoDateOnly((updated as any).date));
    draftStatus.set((updated as any).status ?? TrainingSessionStatus._0);
    draftNotes.set(String((updated as any).notes ?? ''));

    return true;
  } catch (e: any) {
    errorMsg.set(
      e?.body?.title || e?.body?.detail || e?.message || 'Failed to update session.'
    );
    return false;
  } finally {
    isLoading.set(false);
  }
}

export async function deleteSession(sessionId: string): Promise<boolean> {
  if (!sessionId) return false;

  isLoading.set(true);
  errorMsg.set('');

  try {
    const client = new DeleteTrainingSessionEndpointClient();
    await client.delete(sessionId as any);
    
    session.set(null);
    movements.set([]);
    
    return true;
  } catch (e: any) {
    errorMsg.set(
      e?.body?.title || e?.body?.detail || e?.message || 'Failed to delete session.'
    );
    return false;
  } finally {
    isLoading.set(false);
  }
}

// ---------- Movement CRUD ----------

export async function addMovement(sessionId: string): Promise<void> {
  const s = get(session);
  if (!s) return;

  const baseId = get(newMovementBaseId);
  const equipId = get(newEquipmentId);
  const notes = get(newMovementNotes);

  if (!baseId || !equipId) {
    errorMsg.set('Pick a movement and equipment first.');
    return;
  }

  errorMsg.set('');

  try {
    const client = new CreateMovementEndpointClient();
    const req: CreateMovementRequest = {
      trainingSessionID: sessionId,
      movementData: {
        equipmentID: equipId,
        movementBaseID: baseId,
        movementModifierID: null,
      },
      notes: (notes ?? '').trim(),
    } as any;

    const created = await client.post(req as any);
    movements.update((movs) => [...movs, created as MovementDTO]);

    newMovementNotes.set('');
    newModifierText.set('');
  } catch (e: any) {
    errorMsg.set(e?.body?.title || e?.body?.detail || e?.message || 'Failed to add movement.');
  }
}

export async function addMovementQuick(sessionId: string): Promise<void> {
  const s = get(session);
  if (!s) return;

  const baseId = get(newMovementBaseId);
  const equipId = get(newEquipmentId);
  const modifier = get(newMovementModifier).trim() || null;

  if (!baseId || !equipId) return;

  errorMsg.set('');

  try {
    const client = new CreateMovementEndpointClient();
    const req: CreateMovementRequest = {
      trainingSessionID: sessionId,
      movementData: {
        equipmentID: equipId,
        movementBaseID: baseId,
        movementModifierID: null,
        movementModifierName: modifier,
      },
      notes: '',
    } as any;

    const created = await client.post(req as any);
    movements.update((movs) => [...movs, created as MovementDTO]);
    newMovementModifier.set('');
  } catch (e: any) {
    errorMsg.set(e?.body?.detail || e?.message || 'Failed to add movement.');
  }
}

export async function deleteMovement(movementId: string): Promise<void> {
  if (!movementId) return;

  errorMsg.set('');

  try {
    const client = new DeleteMovementEndpointClient();
    await client.delete(movementId as any);

    movements.update((movs) => movs.filter((m) => idOfMovement(m) !== movementId));

    // If there was a pending reorder, remove the deleted movement from it
    const currentPending = get(pendingOrderIds);
    if (currentPending) {
      const filtered = currentPending.filter((id) => id !== movementId);
      // Only keep pending if there are still movements and order might differ
      pendingOrderIds.set(filtered.length > 0 ? filtered : null);
    }
  } catch (e: any) {
    errorMsg.set(
      e?.body?.title || e?.body?.detail || e?.message || 'Failed to delete movement.'
    );
  }
}

export async function updateMovement(
  movementId: string,
  patch: {
    movementBaseID?: string;
    equipmentID?: string;
    movementModifierName?: string | null;
    notes?: string;
  }
): Promise<void> {
  if (!movementId) return;

  const currentMovements = get(movements);
  const m = currentMovements.find((x) => idOfMovement(x) === movementId);
  if (!m) return;

  const mData = (m as any)?.movementData ?? (m as any)?.movementDataDTO ?? {};

  errorMsg.set('');

  try {
    const client = new UpdateMovementEndpointClient();
    const req: UpdateMovementRequest = {
      movementID: movementId,
      movementData: {
        equipmentID: patch.equipmentID ?? mData?.equipment?.equipmentID ?? '',
        movementBaseID: patch.movementBaseID ?? mData?.movementBase?.movementBaseID ?? '',
        movementModifierName:
          patch.movementModifierName !== undefined
            ? patch.movementModifierName
            : mData?.movementModifier?.name ?? null,
      },
      notes: patch.notes !== undefined ? patch.notes : (m as any).notes ?? '',
      isCompleted: (m as any).isCompleted ?? false,
    } as any;

    const updated = await client.put(req as any);

    movements.update((movs) =>
      movs.map((x) => (idOfMovement(x) === movementId ? (updated as MovementDTO) : x))
    );
  } catch (e: any) {
    errorMsg.set(e?.body?.title || e?.body?.detail || e?.message || 'Failed to update movement.');
  }
}

export async function toggleMovementComplete(movementId: string): Promise<void> {
  if (!movementId) return;

  const currentMovements = get(movements);
  const m = currentMovements.find((x) => idOfMovement(x) === movementId);
  if (!m) return;

  const mData = (m as any)?.movementData ?? (m as any)?.movementDataDTO ?? {};
  const newCompleted = !((m as any).isCompleted ?? false);

  errorMsg.set('');

  try {
    const client = new UpdateMovementEndpointClient();
    const req: UpdateMovementRequest = {
      movementID: movementId,
      movementData: {
        equipmentID: mData?.equipment?.equipmentID ?? '',
        movementBaseID: mData?.movementBase?.movementBaseID ?? '',
        movementModifierName: mData?.movementModifier?.name ?? null,
      },
      notes: (m as any).notes ?? '',
      isCompleted: newCompleted,
    } as any;

    const updated = await client.put(req as any);

    movements.update((movs) =>
      movs.map((x) => (idOfMovement(x) === movementId ? (updated as MovementDTO) : x))
    );
  } catch (e: any) {
    errorMsg.set(e?.body?.title || e?.body?.detail || e?.message || 'Failed to toggle movement status.');
  }
}

// ---------- Movement Reorder ----------

export function swapMovementsById(aId: string, bId: string): void {
  movements.update((movs) => {
    const aIndex = movs.findIndex((x) => idOfMovement(x) === aId);
    const bIndex = movs.findIndex((x) => idOfMovement(x) === bId);
    if (aIndex < 0 || bIndex < 0) return movs;

    const copy = movs.slice();
    const tmp = copy[aIndex];
    copy[aIndex] = copy[bIndex];
    copy[bIndex] = tmp;
    return copy;
  });
  pendingOrderIds.set(get(movements).map((m) => idOfMovement(m)));
}

// ---------- Lift Set CRUD ----------

export async function addLiftSet(m: MovementDTO, hideRecommended: boolean = false): Promise<void> {
  const movementID = idOfMovement(m);
  if (!movementID) return;

  errorMsg.set('');

  try {
    const client = new CreateLiftSetEntryEndpointClient();
    const req: CreateLiftSetEntryRequest = {
      movementID,
      recommendedReps: hideRecommended ? undefined : 0,
      recommendedWeight: hideRecommended ? undefined : 0,
      recommendedRPE: hideRecommended ? undefined : 0,
      actualReps: 0,
      actualWeight: 0,
      actualRPE: 0,
      weightUnit: get(displayWeightUnit),
    } as any;

    const created = await client.post(req as any);

    movements.update((movs) => {
      const copy = movs.slice();
      const idx = copy.findIndex((x) => idOfMovement(x) === movementID);
      if (idx >= 0) {
        const mm: any = copy[idx];
        mm.liftSets = [...(mm.liftSets ?? []), created];
        copy[idx] = mm;
      }
      return copy;
    });
  } catch (e: any) {
    errorMsg.set(e?.body?.title || e?.body?.detail || e?.message || 'Failed to add lift set.');
  }
}

export async function updateLiftSet(
  m: MovementDTO,
  s: any,
  patch: Partial<any>
): Promise<void> {
  const movementID = idOfMovement(m);
  const setEntryID = setId(s);
  if (!movementID || !setEntryID) return;

  errorMsg.set('');

  try {
    const client = new UpdateLiftSetEntryEndpointClient();
    const req: UpdateLiftSetEntryRequest = {
      setEntryID,
      movementID,
      recommendedReps: s.recommendedReps ?? null,
      recommendedWeight: s.recommendedWeight ?? null,
      recommendedRPE: s.recommendedRPE ?? null,
      actualReps: s.actualReps ?? 0,
      actualWeight: s.actualWeight ?? 0,
      actualRPE: s.actualRPE ?? 0,
      weightUnit: s.weightUnit ?? get(displayWeightUnit),
      ...patch,
    } as any;

    const updated = await client.put(req as any);

    movements.update((movs) => {
      const copy = movs.slice();
      const idx = copy.findIndex((x) => idOfMovement(x) === movementID);
      if (idx >= 0) {
        const mm: any = copy[idx];
        mm.liftSets = (mm.liftSets ?? []).map((x: any) =>
          setId(x) === setEntryID ? updated : x
        );
        copy[idx] = mm;
      }
      return copy;
    });
  } catch (e: any) {
    errorMsg.set(e?.body?.title || e?.body?.detail || e?.message || 'Failed to update lift set.');
  }
}

export async function deleteLiftSet(m: MovementDTO, s: any): Promise<void> {
  const setEntryID = setId(s);
  if (!setEntryID) return;

  errorMsg.set('');

  try {
    const client = new DeleteLiftSetEntryEndpointClient();
    await client.delete(setEntryID as any);

    const movementID = idOfMovement(m);
    movements.update((movs) => {
      const copy = movs.slice();
      const idx = copy.findIndex((x) => idOfMovement(x) === movementID);
      if (idx >= 0) {
        const mm: any = copy[idx];
        mm.liftSets = (mm.liftSets ?? []).filter((x: any) => setId(x) !== setEntryID);
        copy[idx] = mm;
      }
      return copy;
    });
  } catch (e: any) {
    errorMsg.set(e?.body?.title || e?.body?.detail || e?.message || 'Failed to delete lift set.');
  }
}

// ---------- DT Set CRUD ----------

function saneIntervalTypeDefault() {
  return 0;
}

function saneDistanceUnitDefault() {
  return 0;
}

export async function addDtSet(m: MovementDTO): Promise<void> {
  const movementID = idOfMovement(m);
  if (!movementID) return;

  errorMsg.set('');

  try {
    const client = new CreateDTSetEntryEndpointClient();
    const req: CreateDTSetEntryRequest = {
      movementID,
      recommendedDistance: 0,
      actualDistance: 0,
      intervalDuration: '00:00:00',
      targetPace: '00:00:00',
      actualPace: '00:00:00',
      recommendedDuration: '00:00:00',
      actualDuration: '00:00:00',
      recommendedRest: '00:00:00',
      actualRest: '00:00:00',
      intervalType: saneIntervalTypeDefault() as any,
      distanceUnit: saneDistanceUnitDefault() as any,
      actualRPE: 0,
    } as any;

    const created = await client.post(req as any);

    movements.update((movs) => {
      const copy = movs.slice();
      const idx = copy.findIndex((x) => idOfMovement(x) === movementID);
      if (idx >= 0) {
        const mm: any = copy[idx];
        mm.distanceTimeSets = [...(mm.distanceTimeSets ?? []), created];
        copy[idx] = mm;
      }
      return copy;
    });
  } catch (e: any) {
    errorMsg.set(
      e?.body?.title || e?.body?.detail || e?.message || 'Failed to add distance/time set.'
    );
  }
}

export async function updateDtSet(m: MovementDTO, s: any, patch: Partial<any>): Promise<void> {
  const movementID = idOfMovement(m);
  const setEntryID = setId(s);
  if (!movementID || !setEntryID) return;

  errorMsg.set('');

  try {
    const client = new UpdateDTSetEntryEndpointClient();
    const req: UpdateDTSetEntryRequest = {
      setEntryID,
      movementID,
      recommendedDistance: s.recommendedDistance ?? 0,
      actualDistance: s.actualDistance ?? 0,
      intervalDuration: s.intervalDuration ?? '00:00:00',
      targetPace: s.targetPace ?? '00:00:00',
      actualPace: s.actualPace ?? '00:00:00',
      recommendedDuration: s.recommendedDuration ?? '00:00:00',
      actualDuration: s.actualDuration ?? '00:00:00',
      recommendedRest: s.recommendedRest ?? '00:00:00',
      actualRest: s.actualRest ?? '00:00:00',
      intervalType: s.intervalType ?? saneIntervalTypeDefault(),
      distanceUnit: s.distanceUnit ?? saneDistanceUnitDefault(),
      actualRPE: s.actualRPE ?? 0,
      ...patch,
    } as any;

    await client.put(req as any);
  } catch (e: any) {
    errorMsg.set(
      e?.body?.title || e?.body?.detail || e?.message || 'Failed to update distance/time set.'
    );
  }
}

export async function deleteDtSet(m: MovementDTO, s: any): Promise<void> {
  const setEntryID = setId(s);
  if (!setEntryID) return;

  errorMsg.set('');

  try {
    const client = new DeleteDTSetEntryEndpointClient();
    await client.delete(setEntryID as any);

    const movementID = idOfMovement(m);
    movements.update((movs) => {
      const copy = movs.slice();
      const idx = copy.findIndex((x) => idOfMovement(x) === movementID);
      if (idx >= 0) {
        const mm: any = copy[idx];
        mm.distanceTimeSets = (mm.distanceTimeSets ?? []).filter(
          (x: any) => setId(x) !== setEntryID
        );
        copy[idx] = mm;
      }
      return copy;
    });
  } catch (e: any) {
    errorMsg.set(
      e?.body?.title || e?.body?.detail || e?.message || 'Failed to delete distance/time set.'
    );
  }
}

// ---------- Reset Store ----------

export function resetSessionStore(): void {
  session.set(null);
  movements.set([]);
  movementBases.set([]);
  movementModifiers.set([]);
  equipments.set([]);
  isLoading.set(false);
  errorMsg.set('');
  isEditing.set(false);
  draftDate.set('');
  draftStatus.set(TrainingSessionStatus._0);
  draftNotes.set('');
  pendingOrderIds.set(null);
  dragFromId.set(null);
  dragOverId.set(null);
  newMovementBaseId.set('');
  newEquipmentId.set('');
  newMovementNotes.set('');
  newModifierText.set('');
  dtFieldVisibility.set({});
  dtFieldPickerOpen.set({});
}
