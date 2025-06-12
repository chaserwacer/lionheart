// Weight unit enum
export type WeightUnit = 'Kilograms' | 'Pounds';

export type SetEntry = {
  setEntryID: string;
  movementID: string;
  recommendedReps: number;
  recommendedWeight: number;
  recommendedRPE: number;
  weightUnit: WeightUnit;
  actualReps: number;
  actualWeight: number;
  actualRPE: number;
};

export type MovementBase = {
  movementBaseID: string;
  name: string;
};

export type MovementModifier = {
  movementModifierID: string;
  name: string;
  equipment: string;
  duration: number;
};

export type Movement = {
  movementID: string;
  trainingSessionID: string;
  movementBaseID: string;
  movementBase: MovementBase;
  movementModifierID?: string;
  movementModifier?: MovementModifier;
  sets: SetEntry[];
  notes: string;
  completed: boolean;
  removed: boolean;
};

export type SessionStatus = 'Planned' | 'InProgress' | 'Completed' | 'Skipped';

export type TrainingSession = {
  sessionID: string;
  programID: string;
  date: string; // ISO string
  movements: Movement[];
  status: SessionStatus;
};

export type Program = {
  programID: string;
  userID: string;
  title: string;
  startDate: string;
  nextTrainingSessionDate: string;
  endDate: string;
  trainingSessions: TrainingSession[];
  tags: string[];
};
