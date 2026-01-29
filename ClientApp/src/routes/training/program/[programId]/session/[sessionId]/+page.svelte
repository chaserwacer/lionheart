<script lang="ts">
  import { page } from "$app/stores";
  import { goto } from "$app/navigation";
  import { onMount } from "svelte";

  import {
    GetTrainingSessionEndpointClient,
    UpdateTrainingSessionEndpointClient,
    UpdateTrainingSessionRequest,
    DeleteMovementEndpointClient,
    UpdateMovementOrderEndpointClient,
    UpdateMovementOrderRequest,
    TrainingSessionDTO,
    TrainingSessionStatus,
    MovementDTO,
    GetMovementBasesEndpointClient,
    GetEquipmentsEndpointClient,
    MovementBaseDTO,
    EquipmentDTO,
    CreateMovementEndpointClient,
    CreateMovementRequest,

    // sets
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

    // enums
    WeightUnit,
  } from "$lib/api/ApiClient";

  let programId = "";
  let sessionId = "";
  $: ({ programId, sessionId } = $page.params as any);

  // ---------- state ----------
  let session: TrainingSessionDTO | null = null;
  let loading = true;
  let errorMsg = "";

  let movementBases: MovementBaseDTO[] = [];
  let equipments: EquipmentDTO[] = [];

  let isEditing = false;

  // drafts (session)
  let draftDate = ""; // YYYY-MM-DD
  let draftStatus: TrainingSessionStatus = TrainingSessionStatus._0;
  let draftNotes = "";

  // movement list (canonical for reorder + UI)
  let movements: MovementDTO[] = [];
  let pendingOrderIds: string[] | null = null;

  // drag swap
  let dragFromId: string | null = null;
  let dragOverId: string | null = null;

  // add-movement draft
  let newMovementBaseId = "";
  let newEquipmentId = "";
  let newMovementNotes = "";
  let newModifierText = ""; // groundwork only (free text, not persisted yet)

  // session-level weight display toggle (requested)
  let displayWeightUnit: WeightUnit = WeightUnit._0; // assume _0 = KG, _1 = LB

  const SESSION_STATUSES = [
    TrainingSessionStatus._0,
    TrainingSessionStatus._1,
    TrainingSessionStatus._2,
    TrainingSessionStatus._3,
    TrainingSessionStatus._4,
  ];
  type DtFieldKey =
    | "recommendedDistance"
    | "actualDistance"
    | "intervalDuration"
    | "targetPace"
    | "actualPace"
    | "recommendedDuration"
    | "actualDuration"
    | "recommendedRest"
    | "actualRest"
    | "intervalType"
    | "distanceUnit"
    | "actualRPE";

  const DT_FIELD_LABELS: Record<DtFieldKey, string> = {
    recommendedDistance: "Rec Dist",
    actualDistance: "Act Dist",
    intervalDuration: "Interval",
    targetPace: "Target Pace",
    actualPace: "Actual Pace",
    recommendedDuration: "Rec Dur",
    actualDuration: "Act Dur",
    recommendedRest: "Rec Rest",
    actualRest: "Act Rest",
    intervalType: "Type",
    distanceUnit: "Unit",
    actualRPE: "RPE",
  };

  let dtFieldVisibility: Record<string, Record<DtFieldKey, boolean>> = {};
  let dtFieldPickerOpen: Record<string, boolean> = {};
  type SetKind = "none" | "lift" | "dt";

  // ---------- helpers: ids / safe access ----------
  function idOfMovement(m: MovementDTO): string {
    return String((m as any)?.movementID ?? (m as any)?.movementId ?? "");
  }

  function movementData(m: MovementDTO): any {
    return (m as any)?.movementData ?? (m as any)?.movementDataDTO ?? null;
  }

  function movementBaseName(m: MovementDTO): string {
    return String(movementData(m)?.movementBase?.name ?? "Movement");
  }

  function movementEquipmentName(m: MovementDTO): string {
    return String(movementData(m)?.equipment?.name ?? "—");
  }

  function movementModifierName(m: MovementDTO): string {
    return String(movementData(m)?.movementModifier?.name ?? "—");
  }

  function movementNotes(m: MovementDTO): string {
    return String((m as any)?.notes ?? "");
  }

  function movementCompleted(m: MovementDTO): boolean {
    return Boolean((m as any)?.isCompleted ?? false);
  }

  function liftSets(m: MovementDTO): any[] {
    return ((m as any)?.liftSets ?? []) as any[];
  }

  function dtSets(m: MovementDTO): any[] {
    return ((m as any)?.distanceTimeSets ?? (m as any)?.dtSets ?? []) as any[];
  }

  function movementSetCount(m: MovementDTO): number {
    return liftSets(m).length + dtSets(m).length;
  }

  function setId(s: any): string {
    return String(s?.setEntryID ?? s?.setEntryId ?? "");
  }

  // ---------- helpers: formatting ----------
  function toIsoDateOnly(raw: any): string {
    if (!raw) return "";
    if (typeof raw === "string") return raw.slice(0, 10);
    if (raw instanceof Date) return raw.toISOString().slice(0, 10);

    // DateOnly commonly arrives shaped like { year, month, day }
    if (typeof raw === "object" && raw.year && raw.month && raw.day) {
      const y = String(raw.year).padStart(4, "0");
      const m = String(raw.month).padStart(2, "0");
      const d = String(raw.day).padStart(2, "0");
      return `${y}-${m}-${d}`;
    }

    return "";
  }

  function dateToUS(raw: any): string {
    const iso = toIsoDateOnly(raw);
    if (!iso) return "";
    const d = new Date(`${iso}T12:00:00`);
    if (Number.isNaN(d.getTime())) return String(raw);

    const weekday = d.toLocaleDateString("en-US", { weekday: "long" });
    const md = d.toLocaleDateString("en-US", {
      month: "2-digit",
      day: "2-digit",
    }); // MM/DD
    return `${weekday} ${md}`;
  }

  function statusLabel(s: TrainingSessionStatus): string {
    switch (s) {
      case TrainingSessionStatus._0:
        return "Planned";
      case TrainingSessionStatus._1:
        return "Active";
      case TrainingSessionStatus._2:
        return "Completed";
      case TrainingSessionStatus._3:
        return "Skipped";
      case TrainingSessionStatus._4:
        return "AI Modified";
      default:
        return "Unknown";
    }
  }

  function weightUnitLabel(u: WeightUnit): string {
    // adjust if your enum maps differently
    return u === WeightUnit._1 ? "lb" : "kg";
  }

  function lbToKg(lb: number): number {
    return lb / 2.2046226218;
  }
  function kgToLb(kg: number): number {
    return kg * 2.2046226218;
  }

  function displayWeight(
    value: number | null | undefined,
    fromUnit: WeightUnit,
  ): string {
    if (value === null || value === undefined) return "—";
    const v = Number(value);
    if (!Number.isFinite(v)) return "—";

    if (fromUnit === displayWeightUnit) return String(roundTo(v, 2));
    // convert
    const converted = fromUnit === WeightUnit._1 ? lbToKg(v) : kgToLb(v);
    return String(roundTo(converted, 2));
  }

  function roundTo(v: number, dp: number): number {
    const p = Math.pow(10, dp);
    return Math.round(v * p) / p;
  }

  function parseNumberOrZero(v: any): number {
    const n = Number(v);
    return Number.isFinite(n) ? n : 0;
  }

  // time helpers for DT sets (display + edit)
  function pad2(n: number): string {
    return String(n).padStart(2, "0");
  }

  function formatTimeSpan(ts: any): string {
    // backend likely serializes TimeSpan as "hh:mm:ss" or "mm:ss"
    if (ts === null || ts === undefined) return "—";
    if (typeof ts === "string") return ts;
    // sometimes comes as { ticks } etc — fallback
    return String(ts);
  }

  // ---------- navigation ----------
  function goBack() {
    goto("/training");
  }

  // ---------- load ----------
  async function load() {
    loading = true;
    errorMsg = "";
    session = null;
    isEditing = false;
    pendingOrderIds = null;

    try {
      const sessionClient = new GetTrainingSessionEndpointClient();
      const basesClient = new GetMovementBasesEndpointClient();
      const equipClient = new GetEquipmentsEndpointClient();

      const [s, bases, eqs] = await Promise.all([
        sessionClient.get(sessionId, programId),
        basesClient.get(),
        equipClient.get(),
      ]);

      session = s;
      movements = (session.movements ?? []) as any[];
      for (const m of movements as any[]) {
        const id = idOfMovement(m);
        if (!id) continue;

        // sane defaults: show only what most people actually type
        dtFieldVisibility[id] ??= {
          actualDistance: true,
          intervalDuration: true,
          actualPace: true,
          actualDuration: true,
          actualRest: true,
          actualRPE: true,

          // hidden by default
          recommendedDistance: false,
          targetPace: false,
          recommendedDuration: false,
          recommendedRest: false,
          intervalType: false,
          distanceUnit: false,
        } as any;

        dtFieldPickerOpen[id] ??= false;
      }

      movementBases = (bases ?? []) as any[];
      equipments = (eqs ?? []) as any[];

      // default add-movement draft
      newMovementBaseId = movementBases[0]?.movementBaseID
        ? String(movementBases[0].movementBaseID)
        : "";
      newEquipmentId = equipments[0]?.equipmentID
        ? String(equipments[0].equipmentID)
        : "";
      newMovementNotes = "";
      newModifierText = "";

      // seed session drafts (useful even in view mode)
      draftDate = toIsoDateOnly((session as any).date);
      draftStatus = (session as any).status ?? TrainingSessionStatus._0;
      draftNotes = String((session as any).notes ?? "");
    } catch (e: any) {
      errorMsg =
        e?.body?.title ||
        e?.body?.detail ||
        e?.message ||
        "Failed to load session.";
    } finally {
      loading = false;
    }
  }

  // ---------- edit mode ----------
  function enterEdit() {
    if (!session) return;
    isEditing = true;

    draftDate = toIsoDateOnly((session as any).date);
    draftStatus = (session as any).status ?? TrainingSessionStatus._0;
    draftNotes = String((session as any).notes ?? "");

    movements = (session.movements ?? []) as any[];
    pendingOrderIds = null;
  }

  function cancelEdit() {
    load(); // revert everything
  }

  async function addMovementQuick() {
  if (!session) return;
  if (!newMovementBaseId || !newEquipmentId) return;

  loading = true;
  errorMsg = "";
  try {
    const client = new CreateMovementEndpointClient();
    const req: CreateMovementRequest = {
      trainingSessionID: sessionId,
      movementData: {
        equipmentID: newEquipmentId,
        movementBaseID: newMovementBaseId,
        movementModifierID: null,
      },
      notes: "",
    } as any;

    const created = await client.post(req as any);
    movements = [...movements, created as any];
  } catch (e: any) {
    errorMsg = e?.body?.detail || e?.message || "Failed to add movement.";
  } finally {
    loading = false;
  }
}


  async function saveEdits() {
    if (!session) return;

    loading = true;
    errorMsg = "";

    try {
      // update session basics
      const sessionClient = new UpdateTrainingSessionEndpointClient();

      const req: UpdateTrainingSessionRequest = {
        trainingSessionID: (session as any).trainingSessionID ?? sessionId,
        trainingProgramID: (session as any).trainingProgramID ?? programId,
        date: new Date(`${draftDate}T12:00:00`),
        status: draftStatus,
        notes: draftNotes,
        perceivedEffortRatings: (session as any).perceivedEffortRatings ?? null,
      } as any;

      session = await sessionClient.put(req);

      // persist movement order if changed
      if (pendingOrderIds && pendingOrderIds.length > 0) {
        const orderClient = new UpdateMovementOrderEndpointClient();

        const orderReq: UpdateMovementOrderRequest = {
          trainingSessionID: (session as any).trainingSessionID ?? sessionId,
          movements: pendingOrderIds.map((id, idx) => ({
            movementID: id,
            ordering: idx,
          })),
        } as any;

        await orderClient.put(orderReq as any);
      }

      isEditing = false;
      pendingOrderIds = null;
      location.reload();
    } catch (e: any) {
      errorMsg =
        e?.body?.title ||
        e?.body?.detail ||
        e?.message ||
        "Failed to save changes.";
    } finally {
      loading = false;
    }
  }

  async function updateLiftSetActuals(
    setEntryId: string,
    patch: Partial<UpdateLiftSetEntryRequest>,
  ) {
    loading = true;
    errorMsg = "";
    try {
      const client = new UpdateLiftSetEntryEndpointClient();
      const req: UpdateLiftSetEntryRequest = {
        setEntryID: setEntryId,
        ...patch,
      } as any;
      await client.put(req as any);
    } catch (e: any) {
      errorMsg = e?.body?.detail || e?.message || "Failed to update set.";
    } finally {
      loading = false;
    }
  }

  // ---------- movement reorder (swap on drop) ----------
  function onDragStartMovement(e: DragEvent, m: MovementDTO) {
    const id = idOfMovement(m);
    if (!id) return;

    dragFromId = id;
    dragOverId = null;

    const el = e.currentTarget as HTMLElement | null;
    if (el && e.dataTransfer) {
      const rect = el.getBoundingClientRect();
      e.dataTransfer.setDragImage(el, Math.round(rect.width / 2), 10);
      e.dataTransfer.effectAllowed = "move";
    }
  }

  function onDragEnterMovement(e: DragEvent, m: MovementDTO) {
    e.preventDefault();
    const id = idOfMovement(m);
    if (!id || !dragFromId || id === dragFromId) return;
    dragOverId = id;
  }

  function onDragOverMovement(e: DragEvent, m: MovementDTO) {
    e.preventDefault();
    const id = idOfMovement(m);
    if (!id || !dragFromId || id === dragFromId) return;
    dragOverId = id;
  }

  function onDragLeaveMovement(_e: DragEvent, m: MovementDTO) {
    const id = idOfMovement(m);
    if (dragOverId === id) dragOverId = null;
  }

  function onDragEndMovement() {
    dragFromId = null;
    dragOverId = null;
  }

  function swapMovementsById(aId: string, bId: string) {
    const aIndex = movements.findIndex((x: any) => idOfMovement(x) === aId);
    const bIndex = movements.findIndex((x: any) => idOfMovement(x) === bId);
    if (aIndex < 0 || bIndex < 0) return;

    const copy = movements.slice();
    const tmp = copy[aIndex];
    copy[aIndex] = copy[bIndex];
    copy[bIndex] = tmp;

    movements = copy;
    pendingOrderIds = movements.map((m: any) => idOfMovement(m));
  }

  function onDropOnMovement(e: DragEvent, m: MovementDTO) {
    e.preventDefault();
    const toId = idOfMovement(m);
    const fromId = dragFromId;
    if (!fromId || !toId || fromId === toId) return;

    swapMovementsById(fromId, toId);
    dragFromId = null;
    dragOverId = null;
  }

  // ---------- movement CRUD ----------
  async function deleteMovement(movementId?: string) {
    if (!movementId) return;
    if (!confirm("Remove this movement? This cannot be undone.")) return;

    loading = true;
    errorMsg = "";

    try {
      const client = new DeleteMovementEndpointClient();
      await client.delete(movementId as any);

      movements = movements.filter((m: any) => idOfMovement(m) !== movementId);
      pendingOrderIds = movements.map((m: any) => idOfMovement(m));
    } catch (e: any) {
      errorMsg =
        e?.body?.title ||
        e?.body?.detail ||
        e?.message ||
        "Failed to delete movement.";
    } finally {
      loading = false;
    }
  }

  async function addMovement() {
    if (!session) return;
    if (!newMovementBaseId || !newEquipmentId) {
      errorMsg = "Pick a movement and equipment first.";
      return;
    }

    loading = true;
    errorMsg = "";

    try {
      const client = new CreateMovementEndpointClient();

      const req: CreateMovementRequest = {
        trainingSessionID: sessionId,
        movementData: {
          equipmentID: newEquipmentId,
          movementBaseID: newMovementBaseId,
          movementModifierID: null, // endpoints not ready yet
        },
        notes: (newMovementNotes ?? "").trim(),
      } as any;

      const created = await client.post(req as any);

      // groundwork: free-text modifier isn't persisted yet.
      // If you want it visible immediately, we can prefix notes locally (UI-only),
      // but per your ask, I'm leaving it as "groundwork" only.
      movements = [...movements, created as any];
      pendingOrderIds = movements.map((m: any) => idOfMovement(m));

      newMovementNotes = "";
      newModifierText = "";
    } catch (e: any) {
      errorMsg =
        e?.body?.title ||
        e?.body?.detail ||
        e?.message ||
        "Failed to add movement.";
    } finally {
      loading = false;
    }
  }

  // ---------- set entry CRUD ----------
  async function addLiftSet(m: MovementDTO) {
    const movementID = idOfMovement(m);
    if (!movementID) return;

    loading = true;
    errorMsg = "";

    try {
      const client = new CreateLiftSetEntryEndpointClient();

      // recommended fields: null by default; actual editable
      const req: CreateLiftSetEntryRequest = {
        movementID,
        recommendedReps: null,
        recommendedWeight: null,
        recommendedRPE: null,
        actualReps: 0,
        actualWeight: 0,
        actualRPE: 0,
        weightUnit: displayWeightUnit,
      } as any;

      const created = await client.post(req as any);

      const copy = movements.slice();
      const idx = copy.findIndex((x: any) => idOfMovement(x) === movementID);
      if (idx >= 0) {
        const mm: any = copy[idx];
        mm.liftSets = [...(mm.liftSets ?? []), created];
        copy[idx] = mm;
        movements = copy;
      }
    } catch (e: any) {
      errorMsg =
        e?.body?.title ||
        e?.body?.detail ||
        e?.message ||
        "Failed to add lift set.";
    } finally {
      loading = false;
    }
  }

  async function updateLiftSet(m: MovementDTO, s: any, patch: Partial<any>) {
    const movementID = idOfMovement(m);
    const setEntryID = setId(s);
    if (!movementID || !setEntryID) return;

    loading = true;
    errorMsg = "";

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
        weightUnit: s.weightUnit ?? displayWeightUnit,
        ...patch,
      } as any;

      const updated = await client.put(req as any);

      // update in-place
      const copy = movements.slice();
      const idx = copy.findIndex((x: any) => idOfMovement(x) === movementID);
      if (idx >= 0) {
        const mm: any = copy[idx];
        mm.liftSets = (mm.liftSets ?? []).map((x: any) =>
          setId(x) === setEntryID ? updated : x,
        );
        copy[idx] = mm;
        movements = copy;
      }
    } catch (e: any) {
      errorMsg =
        e?.body?.title ||
        e?.body?.detail ||
        e?.message ||
        "Failed to update lift set.";
    } finally {
      loading = false;
    }
  }

  async function deleteLiftSet(m: MovementDTO, s: any) {
    const setEntryID = setId(s);
    if (!setEntryID) return;
    if (!confirm("Delete this set?")) return;

    loading = true;
    errorMsg = "";

    try {
      const client = new DeleteLiftSetEntryEndpointClient();
      await client.delete(setEntryID as any);

      const movementID = idOfMovement(m);
      const copy = movements.slice();
      const idx = copy.findIndex((x: any) => idOfMovement(x) === movementID);
      if (idx >= 0) {
        const mm: any = copy[idx];
        mm.liftSets = (mm.liftSets ?? []).filter(
          (x: any) => setId(x) !== setEntryID,
        );
        copy[idx] = mm;
        movements = copy;
      }
    } catch (e: any) {
      errorMsg =
        e?.body?.title ||
        e?.body?.detail ||
        e?.message ||
        "Failed to delete lift set.";
    } finally {
      loading = false;
    }
  }

  async function addDtSet(m: MovementDTO) {
    const movementID = idOfMovement(m);
    if (!movementID) return;

    loading = true;
    errorMsg = "";

    try {
      const client = new CreateDTSetEntryEndpointClient();

      const req: CreateDTSetEntryRequest = {
        movementID,
        // you said "all fields" — start with reasonable defaults
        recommendedDistance: 0,
        actualDistance: 0,
        intervalDuration: "00:00:00",
        targetPace: "00:00:00",
        actualPace: "00:00:00",
        recommendedDuration: "00:00:00",
        actualDuration: "00:00:00",
        recommendedRest: "00:00:00",
        actualRest: "00:00:00",
        intervalType: saneIntervalTypeDefault() as any,
        distanceUnit: saneDistanceUnitDefault() as any,
        actualRPE: 0,
      } as any;

      const created = await client.post(req as any);

      const copy = movements.slice();
      const idx = copy.findIndex((x: any) => idOfMovement(x) === movementID);
      if (idx >= 0) {
        const mm: any = copy[idx];
        mm.distanceTimeSets = [...(mm.distanceTimeSets ?? []), created];
        copy[idx] = mm;
        movements = copy;
      }
    } catch (e: any) {
      errorMsg =
        e?.body?.title ||
        e?.body?.detail ||
        e?.message ||
        "Failed to add distance/time set.";
    } finally {
      loading = false;
    }
  }

  // crude defaults; replace when you confirm your IntervalType/DistanceUnit enums if needed
  function saneIntervalTypeDefault() {
    return 0;
  }
  function saneDistanceUnitDefault() {
    return 0;
  }
  function sessionStatusValue(
    s: TrainingSessionDTO | null,
  ): TrainingSessionStatus {
    // Always returns a valid enum value (defaults to Planned)
    if (!s) return TrainingSessionStatus._0;

    const v: any = (s as any).status;

    // if it already is a valid enum member, return it
    if (v === TrainingSessionStatus._0) return TrainingSessionStatus._0;
    if (v === TrainingSessionStatus._1) return TrainingSessionStatus._1;
    if (v === TrainingSessionStatus._2) return TrainingSessionStatus._2;
    if (v === TrainingSessionStatus._3) return TrainingSessionStatus._3;
    if (v === TrainingSessionStatus._4) return TrainingSessionStatus._4;

    // handle numeric/raw values (0-4)
    const n = Number(v);
    if (n === 0) return TrainingSessionStatus._0;
    if (n === 1) return TrainingSessionStatus._1;
    if (n === 2) return TrainingSessionStatus._2;
    if (n === 3) return TrainingSessionStatus._3;
    if (n === 4) return TrainingSessionStatus._4;

    return TrainingSessionStatus._0;
  }

  function sessionStatusLabel(s: TrainingSessionDTO | null): string {
    return statusLabel(sessionStatusValue(s));
  }

  async function updateDtSet(m: MovementDTO, s: any, patch: Partial<any>) {
    const movementID = idOfMovement(m);
    const setEntryID = setId(s);
    if (!movementID || !setEntryID) return;

    loading = true;
    errorMsg = "";

    try {
      const client = new UpdateDTSetEntryEndpointClient();

      const req: UpdateDTSetEntryRequest = {
        setEntryID,
        movementID,
        recommendedDistance: s.recommendedDistance ?? 0,
        actualDistance: s.actualDistance ?? 0,
        intervalDuration: s.intervalDuration ?? "00:00:00",
        targetPace: s.targetPace ?? "00:00:00",
        actualPace: s.actualPace ?? "00:00:00",
        recommendedDuration: s.recommendedDuration ?? "00:00:00",
        actualDuration: s.actualDuration ?? "00:00:00",
        recommendedRest: s.recommendedRest ?? "00:00:00",
        actualRest: s.actualRest ?? "00:00:00",
        intervalType: s.intervalType ?? saneIntervalTypeDefault(),
        distanceUnit: s.distanceUnit ?? saneDistanceUnitDefault(),
        actualRPE: s.actualRPE ?? 0,
        ...patch,
      } as any;

      const updated = await client.put(req as any);

      const copy = movements.slice();
      const idx = copy.findIndex((x: any) => idOfMovement(x) === movementID);
      if (idx >= 0) {
        const mm: any = copy[idx];
        mm.distanceTimeSets = (mm.distanceTimeSets ?? []).map((x: any) =>
          setId(x) === setEntryID ? updated : x,
        );
        copy[idx] = mm;
        movements = copy;
      }
    } catch (e: any) {
      errorMsg =
        e?.body?.title ||
        e?.body?.detail ||
        e?.message ||
        "Failed to update distance/time set.";
    } finally {
      loading = false;
    }
  }

  async function deleteDtSet(m: MovementDTO, s: any) {
    const setEntryID = setId(s);
    if (!setEntryID) return;
    if (!confirm("Delete this set?")) return;

    loading = true;
    errorMsg = "";

    try {
      const client = new DeleteDTSetEntryEndpointClient();
      await client.delete(setEntryID as any);

      const movementID = idOfMovement(m);
      const copy = movements.slice();
      const idx = copy.findIndex((x: any) => idOfMovement(x) === movementID);
      if (idx >= 0) {
        const mm: any = copy[idx];
        mm.distanceTimeSets = (mm.distanceTimeSets ?? []).filter(
          (x: any) => setId(x) !== setEntryID,
        );
        copy[idx] = mm;
        movements = copy;
      }
    } catch (e: any) {
      errorMsg =
        e?.body?.title ||
        e?.body?.detail ||
        e?.message ||
        "Failed to delete distance/time set.";
    } finally {
      loading = false;
    }
  }
  function sessionDateValue(s: TrainingSessionDTO | null): any {
    // returns whatever the client gave us (DateOnly-ish / string / Date)
    return s ? (s as any).date : null;
  }

  function sessionDateUS(s: TrainingSessionDTO | null): string {
    // uses your existing date formatter
    return dateToUS(sessionDateValue(s));
  }
  function sessionNotesValue(s: TrainingSessionDTO | null): string {
    if (!s) return "";
    return String((s as any).notes ?? "");
  }

  function hasSessionNotes(s: TrainingSessionDTO | null): boolean {
    return sessionNotesValue(s).trim().length > 0;
  }

  function mbId(mb: any): string {
    return String(mb?.movementBaseID ?? mb?.movementBaseId ?? "");
  }
  function mbName(mb: any): string {
    return String(mb?.name ?? "");
  }
  function equipmentId(eq: any): string {
    return String(eq?.equipmentID ?? eq?.equipmentId ?? "");
  }

  function equipmentName(eq: any): string {
    return String(eq?.name ?? "");
  }

  function equipmentLabel(eq: any): string {
    if (!eq) return "";
    const name = equipmentName(eq);
    const enabled = Boolean(eq?.enabled ?? true);
    return enabled ? name : `${name} (disabled)`;
  }

  function setKindFor(m: MovementDTO): SetKind {
    const lift = ((m as any)?.liftSets?.length ?? 0) > 0;
    const dt = ((m as any)?.distanceTimeSets?.length ?? 0) > 0;
    if (lift) return "lift";
    if (dt) return "dt";
    return "none";
  }

  // ---------- mount ----------
  onMount(load);
</script>

<svelte:head>
  <title>Session - Lionheart</title>
</svelte:head>

<div class={`min-h-full bg-base-200 ${isEditing ? "editing" : ""}`}>
  <div class="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
    <!-- Header -->
    <header class="mb-10">
      <button
        on:click={goBack}
        class="flex items-center gap-2 text-base-content/60 hover:text-base-content transition-colors mb-6"
      >
        <span>&larr;</span>
        <span class="text-sm font-mono uppercase tracking-widest"
          >Back to Training</span
        >
      </button>

      <div
        class="flex flex-col sm:flex-row sm:items-end sm:justify-between gap-4"
      >
        <div>
          <h1
            class="text-5xl sm:text-7xl font-display font-black tracking-tightest text-base-content leading-none"
          >
            SESSION
          </h1>

          {#if session}
            <div class="mt-4 flex flex-wrap items-center gap-3">
              <span class="badge badge-lg badge-outline font-mono">
                {sessionStatusLabel(session)}
              </span>
              <span
                class="text-base sm:text-lg font-display font-bold text-base-content/80"
              >
                {sessionDateUS(session)}
              </span>
            </div>
          {:else}
            <p
              class="text-sm font-mono uppercase tracking-widest text-base-content/50 mt-3"
            >
              Session Details
            </p>
          {/if}
        </div>

        <div class="flex flex-wrap items-center gap-2 justify-end">
          <!-- weight unit toggle (requested) -->
          <div class="join">
            <button
              class={"btn btn-sm join-item " +
                (displayWeightUnit === WeightUnit._0
                  ? "btn-primary"
                  : "btn-outline")}
              on:click={() => (displayWeightUnit = WeightUnit._0)}
              type="button"
            >
              KG
            </button>
            <button
              class={"btn btn-sm join-item " +
                (displayWeightUnit === WeightUnit._1
                  ? "btn-primary"
                  : "btn-outline")}
              on:click={() => (displayWeightUnit = WeightUnit._1)}
              type="button"
            >
              LB
            </button>
          </div>

          {#if !isEditing}
            <button class="btn btn-primary px-5 rounded-xl" disabled={!session}>
              Start Session
            </button>
            <button
              class="btn btn-outline px-5 rounded-xl"
              disabled={!session}
              on:click={enterEdit}
            >
              Edit
            </button>
          {:else}
            <button
              class="btn btn-primary px-5 rounded-xl"
              on:click={saveEdits}
              disabled={loading}
            >
              Done
            </button>
            <button
              class="btn btn-ghost px-5 rounded-xl"
              on:click={cancelEdit}
              disabled={loading}
            >
              Cancel
            </button>
          {/if}
        </div>
      </div>
    </header>

    {#if loading}
      <div
        class="card bg-base-100 p-8 border border-base-content/10 rounded-2xl text-lg"
      >
        Loading session...
      </div>
    {:else if errorMsg}
      <div class="alert alert-error rounded-xl">
        <span>{errorMsg}</span>
      </div>
    {:else if session}
      <div
        class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-8 rounded-2xl"
      >
        <!-- session notes always visible -->
        <div class="mb-8">
          {#if !isEditing}
            {#if hasSessionNotes(session)}
              <div
                class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-2"
              >
                Notes
              </div>
              <p
                class="text-base sm:text-lg text-base-content/80 whitespace-pre-wrap leading-relaxed"
              >
                {sessionNotesValue(session)}
              </p>
            {/if}

          {:else}
            <div class="flex flex-wrap items-center gap-3">
              <input
                class="input input-sm input-bordered"
                type="date"
                bind:value={draftDate}
              />
              <select
                class="select select-sm select-bordered"
                bind:value={draftStatus}
              >
                {#each SESSION_STATUSES as s}
                  <option value={s}>{statusLabel(s)}</option>
                {/each}
              </select>
            </div>

            <div class="mt-4">
              <div
                class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-2"
              >
                Notes
              </div>
              <textarea
                class="textarea textarea-bordered w-full text-base"
                rows="4"
                bind:value={draftNotes}
                placeholder="Add notes for this session..."
              />
            </div>
          {/if}
        </div>

        <!-- Movements -->
        {#if (movements?.length ?? 0) === 0}
          <div class="p-6 bg-base-200 rounded-xl">
            <div class="font-display font-black text-xl mb-2">
              No movements yet
            </div>
            <div class="text-base text-base-content/60">
              This session is empty right now.
            </div>
          </div>
        {:else}
          <div class="space-y-3">
            {#each movements as m (idOfMovement(m))}
              <div
                class={"p-5 rounded-2xl bg-base-200 border border-base-content/10 " +
                  (isEditing ? "wiggle " : "") +
                  (dragOverId === idOfMovement(m) ? "swap-hover " : "") +
                  (dragFromId === idOfMovement(m) ? "swap-dragging " : "")}
                draggable={isEditing}
                on:dragstart={(e) => isEditing && onDragStartMovement(e, m)}
                on:dragenter={(e) => isEditing && onDragEnterMovement(e, m)}
                on:dragover={(e) => isEditing && onDragOverMovement(e, m)}
                on:dragleave={(e) => isEditing && onDragLeaveMovement(e, m)}
                on:drop={(e) => isEditing && onDropOnMovement(e, m)}
                on:dragend={() => isEditing && onDragEndMovement()}
              >
                <div class="flex items-start justify-between gap-4">
                  <div class="min-w-0">
                    <div
                      class="text-2xl sm:text-3xl font-display font-black leading-tight"
                    >
                      {movementBaseName(m)}
                    </div>

                    <div class="mt-2 flex flex-wrap gap-2">
                      <span class="badge badge-outline font-mono">
                        Equipment: {movementEquipmentName(m)}
                      </span>
                      <span class="badge badge-outline font-mono">
                        Modifier: {movementModifierName(m)}
                      </span>
                      <span
                        class={"badge font-mono " +
                          (movementCompleted(m)
                            ? "badge-success"
                            : "badge-ghost")}
                      >
                        {movementCompleted(m) ? "Completed" : "Not completed"}
                      </span>
                      <span class="badge badge-ghost font-mono">
                        Sets: {movementSetCount(m)}
                      </span>
                    </div>

                    {#if movementNotes(m)}
                      <div
                        class="mt-3 text-base sm:text-lg text-base-content/80 whitespace-pre-wrap leading-relaxed"
                      >
                        {movementNotes(m)}
                      </div>
                    {/if}
                  </div>

                  {#if isEditing}
                    <div class="flex items-center gap-2">
                      <button
                        type="button"
                        class="btn btn-sm btn-outline btn-error rounded-xl"
                        on:click={() => deleteMovement(idOfMovement(m))}
                      >
                        Delete
                      </button>
                    </div>
                  {/if}
                </div>

                <!-- SETS: always visible -->
                <div class="mt-5 grid grid-cols-1 gap-4">
                  <!-- Lift Sets -->
                  <div
                    class="p-4 rounded-xl bg-base-100 border border-base-content/10"
                  >
                    <div class="flex items-center justify-between gap-3 mb-3">
                      <div
                        class="text-sm font-mono uppercase tracking-widest text-base-content/60"
                      >
                        Lift Sets
                      </div>
                      {#if isEditing}
                        {#if setKindFor(m) === "none"}
                          <button
                            class="btn btn-xs btn-outline"
                            on:click={() => addLiftSet(m)}>Add Lift Set</button
                          >
                          <button
                            class="btn btn-xs btn-outline"
                            on:click={() => addDtSet(m)}>Add DT Set</button
                          >
                        {:else if setKindFor(m) === "lift"}
                          <button
                            class="btn btn-xs btn-outline"
                            on:click={() => addLiftSet(m)}>Add Lift Set</button
                          >
                        {:else}
                          <button
                            class="btn btn-xs btn-outline"
                            on:click={() => addDtSet(m)}>Add DT Set</button
                          >
                        {/if}
                      {/if}
                    </div>

                    {#if liftSets(m).length === 0}
                      <div class="text-base text-base-content/60">
                        No lift sets.
                      </div>
                    {:else}
                      <div class="overflow-x-auto">
                        <table class="table table-sm">
                          <thead>
                            <tr
                              class="text-xs font-mono uppercase tracking-widest text-base-content/50"
                            >
                              <th>#</th>
                              <th>Rec Reps</th>
                              <th>Rec Wt</th>
                              <th>Rec RPE</th>
                              <th>Act Reps</th>
                              <th
                                >Act Wt ({weightUnitLabel(
                                  displayWeightUnit,
                                )})</th
                              >
                              <th>Act RPE</th>
                              {#if isEditing}<th></th>{/if}
                            </tr>
                          </thead>
                          <tbody>
                            {#each liftSets(m) as s, i (setId(s))}
                              <tr>
                                <td class="font-mono">{i + 1}</td>
                                <td>{s.recommendedReps ?? "—"}</td>
                                <td>
                                  {#if s.recommendedWeight == null}
                                    —
                                  {:else}
                                    {displayWeight(
                                      s.recommendedWeight,
                                      s.weightUnit,
                                    )}
                                  {/if}
                                </td>
                                <td>{s.recommendedRPE ?? "—"}</td>

                                {#if !isEditing}
                                  <td>
                                    <input
                                      type="number"
                                      class="input input-xs input-bordered w-16 font-semibold"
                                      value={s.actualReps}
                                      on:change={(e) =>
                                        updateLiftSetActuals(s.setEntryID, {
                                          actualReps: Number(
                                            e.currentTarget.value,
                                          ),
                                        })}
                                    />
                                  </td>

                                  <td>
                                    <input
                                      type="number"
                                      class="input input-xs input-bordered w-20 font-semibold"
                                      value={s.actualWeight}
                                      on:change={(e) =>
                                        updateLiftSetActuals(s.setEntryID, {
                                          actualWeight: Number(
                                            e.currentTarget.value,
                                          ),
                                        })}
                                    />
                                  </td>

                                  <td>
                                    <input
                                      type="number"
                                      step="0.5"
                                      class="input input-xs input-bordered w-16 font-semibold"
                                      value={s.actualRPE}
                                      on:change={(e) =>
                                        updateLiftSetActuals(s.setEntryID, {
                                          actualRPE: Number(
                                            e.currentTarget.value,
                                          ),
                                        })}
                                    />
                                  </td>
                                {:else}
                                  <td>
                                    <input
                                      class="input input-sm input-bordered w-20"
                                      type="number"
                                      value={s.actualReps}
                                      on:change={(e) =>
                                        updateLiftSet(m, s, {
                                          actualReps: parseNumberOrZero(
                                            e.currentTarget.value,
                                          ),
                                        })}
                                    />
                                  </td>
                                  <td>
                                    <input
                                      class="input input-sm input-bordered w-28"
                                      type="number"
                                      value={s.actualWeight}
                                      on:change={(e) =>
                                        updateLiftSet(m, s, {
                                          actualWeight: parseNumberOrZero(
                                            e.currentTarget.value,
                                          ),
                                          weightUnit: displayWeightUnit,
                                        })}
                                    />
                                  </td>
                                  <td>
                                    <input
                                      class="input input-sm input-bordered w-24"
                                      type="number"
                                      step="0.5"
                                      value={s.actualRPE}
                                      on:change={(e) =>
                                        updateLiftSet(m, s, {
                                          actualRPE: parseNumberOrZero(
                                            e.currentTarget.value,
                                          ),
                                        })}
                                    />
                                  </td>
                                  <td class="text-right">
                                    <button
                                      class="btn btn-xs btn-outline btn-error"
                                      type="button"
                                      on:click={() => deleteLiftSet(m, s)}
                                    >
                                      Delete
                                    </button>
                                  </td>
                                {/if}
                              </tr>
                            {/each}
                          </tbody>
                        </table>
                      </div>
                    {/if}
                  </div>

                  <!-- Distance/Time Sets -->
                  <div
                    class="p-4 rounded-xl bg-base-100 border border-base-content/10"
                  >
                    <div class="flex items-center justify-between gap-3 mb-3">
                      <div
                        class="text-sm font-mono uppercase tracking-widest text-base-content/60"
                      >
                        Distance / Time Sets
                      </div>
                      {#if isEditing}
                        <button
                          class="btn btn-sm btn-primary rounded-xl"
                          type="button"
                          on:click={() => addDtSet(m)}
                        >
                          + Add DT Set
                        </button>
                      {/if}
                    </div>
                      
                    {#if dtSets(m).length === 0}
                      <div class="text-base text-base-content/60">
                        No distance/time sets.
                      </div>
                    {:else}
                      <div class="space-y-3">
                        {#each dtSets(m) as s, i (setId(s))}
                          <div
                            class="p-3 rounded-xl bg-base-200 border border-base-content/10"
                          >
                            <div class="flex items-start justify-between gap-3">
                              <div class="min-w-0">
                                <div
                                  class="font-mono text-xs uppercase tracking-widest text-base-content/50"
                                >
                                  DT Set {i + 1}
                                </div>
                              </div>
                              {#if isEditing}
                                <button
                                  class="btn btn-xs btn-outline btn-error"
                                  type="button"
                                  on:click={() => deleteDtSet(m, s)}
                                >
                                  Delete
                                </button>
                              {/if}
                            </div>

                            <div
                              class="mt-3 grid grid-cols-1 md:grid-cols-3 gap-3"
                            >
                              <div>
                                <div
                                  class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1"
                                >
                                  Recommended Distance
                                </div>
                                <div class="text-base font-semibold">
                                  {s.recommendedDistance}
                                </div>
                              </div>

                              <div>
                                <div
                                  class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1"
                                >
                                  Actual Distance
                                </div>
                                {#if !isEditing}
                                  <div class="text-base font-semibold">
                                    {s.actualDistance}
                                  </div>
                                {:else}
                                  <input
                                    class="input input-sm input-bordered w-full"
                                    type="number"
                                    value={s.actualDistance}
                                    on:change={(e) =>
                                      updateDtSet(m, s, {
                                        actualDistance: parseNumberOrZero(
                                          e.currentTarget.value,
                                        ),
                                      })}
                                  />
                                {/if}
                              </div>

                              <div>
                                <div
                                  class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1"
                                >
                                  Actual RPE
                                </div>
                                {#if !isEditing}
                                  <div class="text-base font-semibold">
                                    {s.actualRPE}
                                  </div>
                                {:else}
                                  <input
                                    class="input input-sm input-bordered w-full"
                                    type="number"
                                    step="0.5"
                                    value={s.actualRPE}
                                    on:change={(e) =>
                                      updateDtSet(m, s, {
                                        actualRPE: parseNumberOrZero(
                                          e.currentTarget.value,
                                        ),
                                      })}
                                  />
                                {/if}
                              </div>

                              <div>
                                <div
                                  class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1"
                                >
                                  Interval Duration
                                </div>
                                {#if !isEditing}
                                  <div class="text-base font-semibold">
                                    {formatTimeSpan(s.intervalDuration)}
                                  </div>
                                {:else}
                                  <input
                                    class="input input-sm input-bordered w-full"
                                    value={formatTimeSpan(s.intervalDuration)}
                                    placeholder="hh:mm:ss"
                                    on:change={(e) =>
                                      updateDtSet(m, s, {
                                        intervalDuration: e.currentTarget.value,
                                      })}
                                  />
                                {/if}
                              </div>

                              <div>
                                <div
                                  class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1"
                                >
                                  Target Pace
                                </div>
                                {#if !isEditing}
                                  <div class="text-base font-semibold">
                                    {formatTimeSpan(s.targetPace)}
                                  </div>
                                {:else}
                                  <input
                                    class="input input-sm input-bordered w-full"
                                    value={formatTimeSpan(s.targetPace)}
                                    placeholder="hh:mm:ss"
                                    on:change={(e) =>
                                      updateDtSet(m, s, {
                                        targetPace: e.currentTarget.value,
                                      })}
                                  />
                                {/if}
                              </div>

                              <div>
                                <div
                                  class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1"
                                >
                                  Actual Pace
                                </div>
                                {#if !isEditing}
                                  <div class="text-base font-semibold">
                                    {formatTimeSpan(s.actualPace)}
                                  </div>
                                {:else}
                                  <input
                                    class="input input-sm input-bordered w-full"
                                    value={formatTimeSpan(s.actualPace)}
                                    placeholder="hh:mm:ss"
                                    on:change={(e) =>
                                      updateDtSet(m, s, {
                                        actualPace: e.currentTarget.value,
                                      })}
                                  />
                                {/if}
                              </div>

                              <div>
                                <div
                                  class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1"
                                >
                                  Recommended Duration
                                </div>
                                <div class="text-base font-semibold">
                                  {formatTimeSpan(s.recommendedDuration)}
                                </div>
                              </div>

                              <div>
                                <div
                                  class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1"
                                >
                                  Actual Duration
                                </div>
                                {#if !isEditing}
                                  <div class="text-base font-semibold">
                                    {formatTimeSpan(s.actualDuration)}
                                  </div>
                                {:else}
                                  <input
                                    class="input input-sm input-bordered w-full"
                                    value={formatTimeSpan(s.actualDuration)}
                                    placeholder="hh:mm:ss"
                                    on:change={(e) =>
                                      updateDtSet(m, s, {
                                        actualDuration: e.currentTarget.value,
                                      })}
                                  />
                                {/if}
                              </div>

                              <div>
                                <div
                                  class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1"
                                >
                                  Recommended Rest
                                </div>
                                <div class="text-base font-semibold">
                                  {formatTimeSpan(s.recommendedRest)}
                                </div>
                              </div>

                              <div>
                                <div
                                  class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1"
                                >
                                  Actual Rest
                                </div>
                                {#if !isEditing}
                                  <div class="text-base font-semibold">
                                    {formatTimeSpan(s.actualRest)}
                                  </div>
                                {:else}
                                  <input
                                    class="input input-sm input-bordered w-full"
                                    value={formatTimeSpan(s.actualRest)}
                                    placeholder="hh:mm:ss"
                                    on:change={(e) =>
                                      updateDtSet(m, s, {
                                        actualRest: e.currentTarget.value,
                                      })}
                                  />
                                {/if}
                              </div>

                              <div>
                                <div
                                  class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1"
                                >
                                  Interval Type
                                </div>
                                {#if !isEditing}
                                  <div class="text-base font-semibold">
                                    {s.intervalType}
                                  </div>
                                {:else}
                                  <input
                                    class="input input-sm input-bordered w-full"
                                    value={String(s.intervalType)}
                                    on:change={(e) =>
                                      updateDtSet(m, s, {
                                        intervalType: parseNumberOrZero(
                                          e.currentTarget.value,
                                        ),
                                      })}
                                  />
                                {/if}
                              </div>

                              <div>
                                <div
                                  class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1"
                                >
                                  Distance Unit
                                </div>
                                {#if !isEditing}
                                  <div class="text-base font-semibold">
                                    {s.distanceUnit}
                                  </div>
                                {:else}
                                  <input
                                    class="input input-sm input-bordered w-full"
                                    value={String(s.distanceUnit)}
                                    on:change={(e) =>
                                      updateDtSet(m, s, {
                                        distanceUnit: parseNumberOrZero(
                                          e.currentTarget.value,
                                        ),
                                      })}
                                  />
                                {/if}
                                {#if !isEditing}
  <div class="mt-6 p-3 rounded-xl bg-base-200 border border-base-content/10">
    <div class="flex flex-col md:flex-row gap-2 items-stretch md:items-end">
      <div class="flex-1">
        <div
          class="text-[10px] font-mono uppercase tracking-widest text-base-content/50 mb-1"
        >
          Movement
        </div>
        <select
          class="select select-sm select-bordered w-full"
          bind:value={newMovementBaseId}
        >
          {#each movementBases as mb}
            <option value={idOfMovement(mb)}>
              {movementBaseName(mb)}
            </option>
          {/each}
        </select>
      </div>

      <div class="flex-1">
        <div
          class="text-[10px] font-mono uppercase tracking-widest text-base-content/50 mb-1"
        >
          Equipment
        </div>
        <select
          class="select select-sm select-bordered w-full"
          bind:value={newEquipmentId}
        >
          {#each equipments as eq}
            <option value={equipmentId(eq)}>
              {equipmentLabel(eq)}
            </option>
          {/each}
        </select>
      </div>

      <button
        class="btn btn-sm btn-primary rounded-xl"
        on:click={addMovementQuick}
        disabled={loading}
      >
        Add
      </button>
    </div>
  </div>
{/if}

                              </div>
                            </div>
                          </div>
                        {/each}
                      </div>
                    {/if}
                  </div>
                </div>
              </div>
            {/each}
          </div>

          {#if pendingOrderIds}
            <div class="mt-4 text-sm text-base-content/60">
              Order changed — will save when you hit <span class="font-bold"
                >Done</span
              >.
            </div>
          {/if}
        {/if}

        <!-- Add Movement (edit only) -->
        {#if isEditing}
          <div
            class="mb-8 p-5 rounded-2xl bg-base-200 border border-base-content/10"
          >
            <div class="flex items-center justify-between gap-3 mb-4">
              <div
                class="text-xs font-mono uppercase tracking-widest text-base-content/50"
              >
                Add Movement
              </div>
              <div class="text-xs text-base-content/50">
                Movements: <span class="font-bold"
                  >{movements?.length ?? 0}</span
                >
              </div>
            </div>

            <div class="grid grid-cols-1 lg:grid-cols-2 gap-3">
              <div>
                <div
                  class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-2"
                >
                  Movement Base
                </div>
                <select
                  class="select select-bordered w-full"
                  bind:value={newMovementBaseId}
                >
                  {#each movementBases as mb}
                    <option value={mbId(mb)}>{mbName(mb)}</option>
                  {/each}
                </select>
              </div>

              <div>
                <div
                  class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-2"
                >
                  Equipment
                </div>
                <select
                  class="select select-bordered w-full"
                  bind:value={newEquipmentId}
                >
                  {#each equipments as eq}
                    <option value={equipmentId(eq)}>
                      {equipmentLabel(eq)}
                    </option>
                  {/each}
                </select>
              </div>

              <!-- groundwork only -->
              <div class="lg:col-span-2">
                <div
                  class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-2"
                >
                  Modifier (groundwork — not saved yet)
                </div>
                <input
                  class="input input-bordered w-full"
                  placeholder="e.g., Paused / Tempo / Beltless (future MovementModifier)"
                  bind:value={newModifierText}
                />
              </div>

              <div class="lg:col-span-2">
                <div
                  class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-2"
                >
                  Notes
                </div>
                <input
                  class="input input-bordered w-full"
                  placeholder="e.g., cues, constraints, intent"
                  bind:value={newMovementNotes}
                />
              </div>

              <div class="lg:col-span-2 flex justify-end">
                <button
                  class="btn btn-primary rounded-xl px-5"
                  on:click={addMovement}
                  disabled={loading || !newMovementBaseId || !newEquipmentId}
                >
                  Add Movement
                </button>
              </div>
            </div>
          </div>
        {/if}
      </div>
    {:else}
      <div
        class="card bg-base-100 p-8 border border-base-content/10 rounded-2xl"
      >
        Session not found.
      </div>
    {/if}
  </div>
</div>

<style>
  .wiggle {
    animation: none;
  }
  :global(.editing) .wiggle {
    animation: wiggle 0.18s infinite alternate ease-in-out;
    transform-origin: 50% 50%;
  }
  @keyframes wiggle {
    from {
      transform: rotate(-0.6deg);
    }
    to {
      transform: rotate(0.6deg);
    }
  }

  .swap-hover {
    outline: 2px solid rgba(255, 255, 255, 0.35);
    box-shadow: 0 0 0 4px rgba(255, 255, 255, 0.08);
  }

  .swap-dragging {
    opacity: 0.85;
    filter: saturate(1.1);
  }
</style>
