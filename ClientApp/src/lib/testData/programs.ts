
export type SetEntry = {
  recommendedReps: number;
  recommendedRpe?: number;
  recommendedWeight: number;
  // Optional fields for tracking actual performance
  actualWeight?: number;
  actualRpe?: number;
  actualReps?: number;
};

export type Movement = {
  name: string;
  type: string;
  notes?: string;
  sets: SetEntry[]; // Each entry is a single set
};

export type Session = {
  date: string; // Now required
  sessionNumber: number;
  movements: Movement[];
};

export type Block = {
  name: string;
  startDate: string;
  endDate: string;
  sessions: Session[];
};

export type TrainingProgram = {
  name: string;
  type: string;
  nextWorkoutDate: string;
  blocks: Block[];
};

// Example data (shortened for clarity)
export const fakePrograms: TrainingProgram[] = [
  {
    name: "Lionheart Power Block 1",
    type: "Powerlifting",
    nextWorkoutDate: "2025-06-21",
    blocks: [
      {
        name: "Base Strength",
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
                    recommendedReps: 5,
                    recommendedRpe: 8.5,
                    recommendedWeight: 185,
                    actualWeight: 185,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 10,
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
          },
          {
            date: "2025-06-03",
            sessionNumber: 2,
            movements: [
              {
                name: "Deadlift",
                type: "Accessory",
                sets: [
                  {
                    recommendedReps: 5,
                    recommendedRpe: 9.0,
                    recommendedWeight: 135,
                    actualWeight: 135,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 8,
                    recommendedRpe: 7.0,
                    recommendedWeight: 135,
                    actualWeight: 135,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 8,
                    recommendedRpe: 9.0,
                    recommendedWeight: 315,
                    actualWeight: 315,
                    actualRpe: undefined,
                    actualReps: undefined
                  }
                ]
              },
              {
                name: "Lat Pulldown",
                type: "Main Lift",
                sets: [
                  {
                    recommendedReps: 10,
                    recommendedRpe: 7.0,
                    recommendedWeight: 365,
                    actualWeight: 365,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 3,
                    recommendedRpe: 6.5,
                    recommendedWeight: 225,
                    actualWeight: 225,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 10,
                    recommendedRpe: 8.0,
                    recommendedWeight: 135,
                    actualWeight: 135,
                    actualRpe: undefined,
                    actualReps: undefined
                  }
                ]
              }
            ]
          },
          {
            date: "2025-06-05",
            sessionNumber: 3,
            movements: [
              {
                name: "Overhead Press",
                type: "Main Lift",
                sets: [
                  {
                    recommendedReps: 3,
                    recommendedRpe: 8.0,
                    recommendedWeight: 405,
                    actualWeight: 405,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 3,
                    recommendedRpe: 9.0,
                    recommendedWeight: 185,
                    actualWeight: 185,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 5,
                    recommendedRpe: 9.0,
                    recommendedWeight: 275,
                    actualWeight: 275,
                    actualRpe: undefined,
                    actualReps: undefined
                  }
                ]
              }
            ]
          },
          {
            date: "2025-06-07",
            sessionNumber: 4,
            movements: [
              {
                name: "Leg Press",
                type: "Accessory",
                sets: [
                  {
                    recommendedReps: 10,
                    recommendedRpe: 9.0,
                    recommendedWeight: 315,
                    actualWeight: 315,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 8,
                    recommendedRpe: 9.0,
                    recommendedWeight: 95,
                    actualWeight: 95,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 5,
                    recommendedRpe: 9.0,
                    recommendedWeight: 185,
                    actualWeight: 185,
                    actualRpe: undefined,
                    actualReps: undefined
                  }
                ]
              }
            ]
          },
          {
            date: "2025-06-09",
            sessionNumber: 5,
            movements: [
              {
                name: "RDL",
                type: "Accessory",
                sets: [
                  {
                    recommendedReps: 5,
                    recommendedRpe: 7.5,
                    recommendedWeight: 225,
                    actualWeight: 225,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 3,
                    recommendedRpe: 8.5,
                    recommendedWeight: 405,
                    actualWeight: 405,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 3,
                    recommendedRpe: 7.5,
                    recommendedWeight: 365,
                    actualWeight: 365,
                    actualRpe: undefined,
                    actualReps: undefined
                  }
                ]
              },
              {
                name: "Pull-up",
                type: "Main Lift",
                sets: [
                  {
                    recommendedReps: 3,
                    recommendedRpe: 7.5,
                    recommendedWeight: 225,
                    actualWeight: 225,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 5,
                    recommendedRpe: 8.5,
                    recommendedWeight: 275,
                    actualWeight: 275,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 5,
                    recommendedRpe: 8.5,
                    recommendedWeight: 135,
                    actualWeight: 135,
                    actualRpe: undefined,
                    actualReps: undefined
                  }
                ]
              }
            ]
          },
          {
            date: "2025-06-11",
            sessionNumber: 6,
            movements: [
              {
                name: "Leg Press",
                type: "Accessory",
                sets: [
                  {
                    recommendedReps: 3,
                    recommendedRpe: 8.0,
                    recommendedWeight: 275,
                    actualWeight: 275,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 5,
                    recommendedRpe: 6.5,
                    recommendedWeight: 95,
                    actualWeight: 95,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 3,
                    recommendedRpe: 6.5,
                    recommendedWeight: 135,
                    actualWeight: 135,
                    actualRpe: undefined,
                    actualReps: undefined
                  }
                ]
              },
              {
                name: "Overhead Press",
                type: "Main Lift",
                sets: [
                  {
                    recommendedReps: 8,
                    recommendedRpe: 7.5,
                    recommendedWeight: 135,
                    actualWeight: 135,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 8,
                    recommendedRpe: 7.0,
                    recommendedWeight: 95,
                    actualWeight: 95,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 10,
                    recommendedRpe: 9.0,
                    recommendedWeight: 275,
                    actualWeight: 275,
                    actualRpe: undefined,
                    actualReps: undefined
                  }
                ]
              },
              {
                name: "Incline Bench",
                type: "Main Lift",
                sets: [
                  {
                    recommendedReps: 8,
                    recommendedRpe: 8.0,
                    recommendedWeight: 95,
                    actualWeight: 95,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 3,
                    recommendedRpe: 8.0,
                    recommendedWeight: 405,
                    actualWeight: 405,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 5,
                    recommendedRpe: 8.0,
                    recommendedWeight: 315,
                    actualWeight: 315,
                    actualRpe: undefined,
                    actualReps: undefined
                  }
                ]
              }
            ]
          },
          {
            date: "2025-06-13",
            sessionNumber: 7,
            movements: [
              {
                name: "RDL",
                type: "Accessory",
                sets: [
                  {
                    recommendedReps: 3,
                    recommendedRpe: 8.0,
                    recommendedWeight: 405,
                    actualWeight: 405,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 5,
                    recommendedRpe: 8.0,
                    recommendedWeight: 275,
                    actualWeight: 275,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 10,
                    recommendedRpe: 9.5,
                    recommendedWeight: 95,
                    actualWeight: 95,
                    actualRpe: undefined,
                    actualReps: undefined
                  }
                ]
              },
              {
                name: "Squat",
                type: "Main Lift",
                sets: [
                  {
                    recommendedReps: 3,
                    recommendedRpe: 7.5,
                    recommendedWeight: 405,
                    actualWeight: 405,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 8,
                    recommendedRpe: 7.5,
                    recommendedWeight: 225,
                    actualWeight: 225,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 5,
                    recommendedRpe: 9.5,
                    recommendedWeight: 365,
                    actualWeight: 365,
                    actualRpe: undefined,
                    actualReps: undefined
                  }
                ]
              }
            ]
          },
          {
            date: "2025-06-15",
            sessionNumber: 8,
            movements: [
              {
                name: "Pull-up",
                type: "Main Lift",
                sets: [
                  {
                    recommendedReps: 8,
                    recommendedRpe: 8.0,
                    recommendedWeight: 135,
                    actualWeight: 135,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 5,
                    recommendedRpe: 9.5,
                    recommendedWeight: 95,
                    actualWeight: 95,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 10,
                    recommendedRpe: 8.5,
                    recommendedWeight: 95,
                    actualWeight: 95,
                    actualRpe: undefined,
                    actualReps: undefined
                  }
                ]
              },
              {
                name: "Deadlift",
                type: "Accessory",
                sets: [
                  {
                    recommendedReps: 3,
                    recommendedRpe: 9.0,
                    recommendedWeight: 185,
                    actualWeight: 185,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 3,
                    recommendedRpe: 8.0,
                    recommendedWeight: 185,
                    actualWeight: 185,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 5,
                    recommendedRpe: 8.0,
                    recommendedWeight: 405,
                    actualWeight: 405,
                    actualRpe: undefined,
                    actualReps: undefined
                  }
                ]
              },
              {
                name: "RDL",
                type: "Main Lift",
                sets: [
                  {
                    recommendedReps: 8,
                    recommendedRpe: 8.0,
                    recommendedWeight: 185,
                    actualWeight: 185,
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
                  },
                  {
                    recommendedReps: 3,
                    recommendedRpe: 6.5,
                    recommendedWeight: 225,
                    actualWeight: 225,
                    actualRpe: undefined,
                    actualReps: undefined
                  }
                ]
              }
            ]
          },
          {
            date: "2025-06-17",
            sessionNumber: 9,
            movements: [
              {
                name: "Pull-up",
                type: "Accessory",
                sets: [
                  {
                    recommendedReps: 3,
                    recommendedRpe: 9.0,
                    recommendedWeight: 405,
                    actualWeight: 405,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 5,
                    recommendedRpe: 8.0,
                    recommendedWeight: 275,
                    actualWeight: 275,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 5,
                    recommendedRpe: 9.0,
                    recommendedWeight: 225,
                    actualWeight: 225,
                    actualRpe: undefined,
                    actualReps: undefined
                  }
                ]
              }
            ]
          },
          {
            date: "2025-06-19",
            sessionNumber: 10,
            movements: [
              {
                name: "Pull-up",
                type: "Accessory",
                sets: [
                  {
                    recommendedReps: 5,
                    recommendedRpe: 7.5,
                    recommendedWeight: 315,
                    actualWeight: 315,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 3,
                    recommendedRpe: 9.5,
                    recommendedWeight: 405,
                    actualWeight: 405,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 3,
                    recommendedRpe: 6.5,
                    recommendedWeight: 275,
                    actualWeight: 275,
                    actualRpe: undefined,
                    actualReps: undefined
                  }
                ]
              },
              {
                name: "Barbell Row",
                type: "Accessory",
                sets: [
                  {
                    recommendedReps: 8,
                    recommendedRpe: 9.5,
                    recommendedWeight: 185,
                    actualWeight: 185,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 5,
                    recommendedRpe: 6.5,
                    recommendedWeight: 95,
                    actualWeight: 95,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 5,
                    recommendedRpe: 7.0,
                    recommendedWeight: 275,
                    actualWeight: 275,
                    actualRpe: undefined,
                    actualReps: undefined
                  }
                ]
              },
              {
                name: "Overhead Press",
                type: "Accessory",
                sets: [
                  {
                    recommendedReps: 8,
                    recommendedRpe: 7.0,
                    recommendedWeight: 275,
                    actualWeight: 275,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 8,
                    recommendedRpe: 7.5,
                    recommendedWeight: 135,
                    actualWeight: 135,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 3,
                    recommendedRpe: 8.5,
                    recommendedWeight: 315,
                    actualWeight: 315,
                    actualRpe: undefined,
                    actualReps: undefined
                  }
                ]
              },
              {
                name: "RDL",
                type: "Main Lift",
                sets: [
                  {
                    recommendedReps: 3,
                    recommendedRpe: 7.5,
                    recommendedWeight: 275,
                    actualWeight: 275,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 10,
                    recommendedRpe: 6.5,
                    recommendedWeight: 225,
                    actualWeight: 225,
                    actualRpe: undefined,
                    actualReps: undefined
                  },
                  {
                    recommendedReps: 8,
                    recommendedRpe: 8.0,
                    recommendedWeight: 275,
                    actualWeight: 275,
                    actualRpe: undefined,
                    actualReps: undefined
                  }
                ]
              }
            ]
          }
        ]
      }
    ]
  }
];