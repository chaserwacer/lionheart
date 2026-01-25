import { writable, derived } from 'svelte/store';
import {
    GetTrainingProgramsEndpointClient,
    GetTrainingSessionsEndpointClient,
    GetEquipmentsEndpointClient,
    GetMovementBasesEndpointClient,
    CreateTrainingProgramEndpointClient,
    CreateTrainingSessionEndpointClient,
    CreateEquipmentEndpointClient,
    UpdateEquipmentEndpointClient,
    DeleteEquipmentEndpointClient,
    CreateMovementBaseEndpointClient,
    UpdateMovementBaseEndpointClient,
    DeleteMovementBaseEndpointClient,
    GetAllMuscleGroupsAsyncClient,
    CreateEquipmentRequest,
    UpdateEquipmentRequest,
    CreateMovementBaseRequest,
    UpdateMovementBaseRequest,
    type TrainingProgramDTO,
    type TrainingSessionDTO,
    type EquipmentDTO,
    type MovementBaseDTO,
    type MuscleGroup,
    type ITrainedMuscle,
    TrainingSessionStatus,
} from '$lib/api/ApiClient';

const baseUrl = '';

// Stores
export const programs = writable<TrainingProgramDTO[]>([]);
export const sessions = writable<TrainingSessionDTO[]>([]);
export const equipments = writable<EquipmentDTO[]>([]);
export const movementBases = writable<MovementBaseDTO[]>([]);
export const muscleGroups = writable<MuscleGroup[]>([]);
export const isLoading = writable(false);
export const trainingError = writable<string | null>(null);

// Derived stores
export const activeProgram = derived(programs, ($programs) => {
    return $programs.find(p => !p.isCompleted) ?? null;
});

export const allSessions = derived(programs, ($programs) => {
    const all: TrainingSessionDTO[] = [];
    for (const program of $programs) {
        if (program.trainingSessions) {
            all.push(...program.trainingSessions);
        }
    }
    return all.sort((a, b) =>
        new Date(a.date ?? '').getTime() - new Date(b.date ?? '').getTime()
    );
});

export const completedSessions = derived(allSessions, ($allSessions) => {
    return $allSessions.filter(s => s.status === TrainingSessionStatus._2);
});

export const lastCompletedSession = derived(completedSessions, ($completed) => {
    if ($completed.length === 0) return null;
    return $completed[$completed.length - 1];
});

export const upcomingSessions = derived(allSessions, ($allSessions) => {
    const now = new Date();
    return $allSessions.filter(s => {
        const isPlanned = s.status === TrainingSessionStatus._0 || s.status === undefined;
        const isFuture = new Date(s.date ?? '') >= new Date(now.toDateString());
        return isPlanned && isFuture;
    });
});

export const nextSession = derived(upcomingSessions, ($upcoming) => {
    return $upcoming.length > 0 ? $upcoming[0] : null;
});

export const recentSessions = derived(allSessions, ($allSessions) => {
    const twoWeeksAgo = new Date();
    twoWeeksAgo.setDate(twoWeeksAgo.getDate() - 14);

    return $allSessions
        .filter(s => new Date(s.date ?? '') >= twoWeeksAgo)
        .sort((a, b) =>
            new Date(b.date ?? '').getTime() - new Date(a.date ?? '').getTime()
        )
        .slice(0, 10);
});

// Actions
export async function fetchPrograms(): Promise<void> {
    isLoading.set(true);
    trainingError.set(null);

    try {
        const client = new GetTrainingProgramsEndpointClient(baseUrl);
        const data = await client.get();
        programs.set(data);
    } catch (error) {
        console.error('Error fetching programs:', error);
        trainingError.set('Failed to load training programs');
    } finally {
        isLoading.set(false);
    }
}

export async function fetchEquipments(): Promise<void> {
    try {
        const client = new GetEquipmentsEndpointClient(baseUrl);
        const data = await client.get();
        equipments.set(data);
    } catch (error) {
        console.error('Error fetching equipments:', error);
    }
}

export async function fetchMovementBases(): Promise<void> {
    try {
        const client = new GetMovementBasesEndpointClient(baseUrl);
        const data = await client.get();
        movementBases.set(data);
    } catch (error) {
        console.error('Error fetching movement bases:', error);
    }
}

export async function fetchAllTrainingData(): Promise<void> {
    await Promise.all([
        fetchPrograms(),
        fetchEquipments(),
        fetchMovementBases()
    ]);
}

export function getStatusText(status: TrainingSessionStatus | undefined): string {
    switch (status) {
        case TrainingSessionStatus._0: return 'Planned';
        case TrainingSessionStatus._1: return 'Active';
        case TrainingSessionStatus._2: return 'Completed';
        case TrainingSessionStatus._3: return 'Skipped';
        case TrainingSessionStatus._4: return 'AI Modified';
        default: return 'Planned';
    }
}

export function getStatusColor(status: TrainingSessionStatus | undefined): string {
    switch (status) {
        case TrainingSessionStatus._0: return 'badge-ghost';
        case TrainingSessionStatus._1: return 'badge-warning';
        case TrainingSessionStatus._2: return 'badge-success';
        case TrainingSessionStatus._3: return 'badge-error';
        case TrainingSessionStatus._4: return 'badge-info';
        default: return 'badge-ghost';
    }
}

export function formatSessionDate(date: Date | undefined): string {
    if (!date) return 'No date';
    const d = new Date(date);
    const today = new Date();
    const tomorrow = new Date(today);
    tomorrow.setDate(tomorrow.getDate() + 1);
    const yesterday = new Date(today);
    yesterday.setDate(yesterday.getDate() - 1);

    if (d.toDateString() === today.toDateString()) return 'Today';
    if (d.toDateString() === tomorrow.toDateString()) return 'Tomorrow';
    if (d.toDateString() === yesterday.toDateString()) return 'Yesterday';

    const diffTime = today.getTime() - d.getTime();
    const diffDays = Math.floor(diffTime / (1000 * 60 * 60 * 24));

    if (diffDays > 0 && diffDays < 7) return `${diffDays} days ago`;
    if (diffDays < 0 && diffDays > -7) return `in ${Math.abs(diffDays)} days`;

    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
}

export function getSessionTitle(session: TrainingSessionDTO | null): string {
    if (!session) return 'Session';
    if (session.notes) return session.notes.substring(0, 40);
    return formatSessionDate(session.date);
}

// Muscle Groups
export async function fetchMuscleGroups(): Promise<void> {
    try {
        const client = new GetAllMuscleGroupsAsyncClient(baseUrl);
        const data = await client.get();
        muscleGroups.set(data);
    } catch (error) {
        console.error('Error fetching muscle groups:', error);
    }
}

// Equipment CRUD
export async function createEquipment(name: string): Promise<EquipmentDTO | null> {
    try {
        const client = new CreateEquipmentEndpointClient(baseUrl);
        const request = new CreateEquipmentRequest({ name });
        const result = await client.post(request);
        equipments.update(items => [...items, result]);
        return result;
    } catch (error) {
        console.error('Error creating equipment:', error);
        trainingError.set('Failed to create equipment');
        return null;
    }
}

export async function updateEquipment(equipmentID: string, name: string, enabled: boolean): Promise<EquipmentDTO | null> {
    try {
        const client = new UpdateEquipmentEndpointClient(baseUrl);
        const request = new UpdateEquipmentRequest({ equipmentID, name, enabled });
        const result = await client.post(request);
        equipments.update(items =>
            items.map(item => item.equipmentID === equipmentID ? result : item)
        );
        return result;
    } catch (error) {
        console.error('Error updating equipment:', error);
        trainingError.set('Failed to update equipment');
        return null;
    }
}

export async function deleteEquipment(equipmentID: string): Promise<boolean> {
    try {
        const client = new DeleteEquipmentEndpointClient(baseUrl);
        await client.delete(equipmentID);
        equipments.update(items => items.filter(item => item.equipmentID !== equipmentID));
        return true;
    } catch (error) {
        console.error('Error deleting equipment:', error);
        trainingError.set('Failed to delete equipment');
        return false;
    }
}

// Movement Base CRUD
export async function createMovementBase(
    name: string,
    description?: string,
    trainedMuscles?: ITrainedMuscle[]
): Promise<MovementBaseDTO | null> {
    try {
        const client = new CreateMovementBaseEndpointClient(baseUrl);
        const request = CreateMovementBaseRequest.fromJS({ name, description, trainedMuscles });
        const result = await client.post(request);
        movementBases.update(items => [...items, result]);
        return result;
    } catch (error) {
        console.error('Error creating movement base:', error);
        trainingError.set('Failed to create movement base');
        return null;
    }
}

export async function updateMovementBase(
    movementBaseID: string,
    name: string,
    description?: string,
    trainedMuscles?: ITrainedMuscle[]
): Promise<MovementBaseDTO | null> {
    try {
        const client = new UpdateMovementBaseEndpointClient(baseUrl);
        const request = UpdateMovementBaseRequest.fromJS({ movementBaseID, name, description, trainedMuscles });
        const result = await client.post(request);
        movementBases.update(items =>
            items.map(item => item.movementBaseID === movementBaseID ? result : item)
        );
        return result;
    } catch (error) {
        console.error('Error updating movement base:', error);
        trainingError.set('Failed to update movement base');
        return null;
    }
}

export async function deleteMovementBase(movementBaseID: string): Promise<boolean> {
    try {
        const client = new DeleteMovementBaseEndpointClient(baseUrl);
        await client.delete(movementBaseID);
        movementBases.update(items => items.filter(item => item.movementBaseID !== movementBaseID));
        return true;
    } catch (error) {
        console.error('Error deleting movement base:', error);
        trainingError.set('Failed to delete movement base');
        return false;
    }
}
