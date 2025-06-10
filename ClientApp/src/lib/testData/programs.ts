export type SetEntry = {
  recommendedReps: number;
  recommendedRpe?: number;
  recommendedWeight: number;
  actualWeight?: number;
  actualRpe?: number;
  actualReps?: number;
};

export type Movement = {
  name: string;
  type: string;
  notes?: string;
  sets: SetEntry[];
};

export type Session = {
  date: string;
  sessionNumber: number;
  movements: Movement[];
};

export type TrainingProgram = {
  name: string;
  type: string;
  nextWorkoutDate: string;
  startDate: string;
  endDate: string;
  sessions: Session[];
};

export const fakePrograms: TrainingProgram[] = [
  {
    name: "Lionheart Power Block 1",
    type: "Powerlifting",
    nextWorkoutDate: "2025-06-21",
    startDate: "2025-06-01",
    endDate: "2025-06-21",
    sessions: [
      {
        date: "2025-06-01",
        sessionNumber: 1,
        movements: [
          {
            name: "Incline Bench",
            type: "Main Lift",
            sets: [
              {
                recommendedReps: 1,
                recommendedRpe: 8.5,
                recommendedWeight: 185,
                actualWeight: 185,
                actualRpe: undefined,
                actualReps: undefined
              },
              {
                recommendedReps: 5,
                recommendedRpe: 9.5,
                recommendedWeight: 315,
                actualWeight: 315,
                actualRpe: undefined,
                actualReps: undefined
              },
              {
                recommendedReps: 3,
                recommendedRpe: 8.0,
                recommendedWeight: 315,
                actualWeight: 315,
                actualRpe: undefined,
                actualReps: undefined
              },
              {
                recommendedReps: 3,
                recommendedRpe: 8.0,
                recommendedWeight: 315,
                actualWeight: 315,
                actualRpe: undefined,
                actualReps: undefined
              },
              {
                recommendedReps: 3,
                recommendedRpe: 8.0,
                recommendedWeight: 315,
                actualWeight: 315,
                actualRpe: undefined,
                actualReps: undefined
              }
            ]
          },
          {
            name: "Bench Press",
            type: "Accessory",
            sets: [
              {
                recommendedReps: 5,
                recommendedRpe: 9.0,
                recommendedWeight: 315,
                actualWeight: 315,
                actualRpe: undefined,
                actualReps: undefined
              },
              {
                recommendedReps: 5,
                recommendedRpe: 7.5,
                recommendedWeight: 365,
                actualWeight: 365,
                actualRpe: undefined,
                actualReps: undefined
              },
              {
                recommendedReps: 10,
                recommendedRpe: 8.5,
                recommendedWeight: 185,
                actualWeight: 185,
                actualRpe: undefined,
                actualReps: undefined
              }
            ]
          }
        ]
      }
      // ...remaining sessions unchanged for brevity
    ]
  }
];
