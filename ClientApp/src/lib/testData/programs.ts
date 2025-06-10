import type { Program } from '$lib/types/programs';
import { v4 as uuid } from 'uuid';

export const fakePrograms: Program[] = [
  {
    "programID": "a4311237-3f49-42d7-a4b5-51bbb31fb62e",
    "userID": "d4d79367-4ad2-4f95-9264-4a2cb25dbec5",
    "title": "Lionheart Power Block 1",
    "startDate": "2025-06-01",
    "nextTrainingSessionDate": "2025-06-03",
    "endDate": "2025-06-21",
    "tags": [
      "Powerlifting"
    ],
    "trainingSessions": [
      {
        "sessionID": "4302a161-5be7-4d78-8a5e-723893b203eb",
        "programID": "a4311237-3f49-42d7-a4b5-51bbb31fb62e",
        "date": "2025-06-01",
        "movements": [
          {
            "movementID": "071aaf6a-96af-4531-bce0-ceecf13a2ae3",
            "trainingSessionID": "4302a161-5be7-4d78-8a5e-723893b203eb",
            "movementBaseID": "base-1",
            "movementBase": {
              "movementBaseID": "base-1",
              "name": "Exercise 1"
            },
            "movementModifierID": "mod-1",
            "movementModifier": {
              "movementModifierID": "mod-1",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "b4911f22-8bbf-42bd-84c5-c1047bcd4dc6",
                "movementID": "071aaf6a-96af-4531-bce0-ceecf13a2ae3",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "a972eeed-5fbb-4305-bd72-e692c5e3e2d3",
                "movementID": "071aaf6a-96af-4531-bce0-ceecf13a2ae3",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "2e6704d4-98e0-4243-9a87-0bf26a699b58",
                "movementID": "071aaf6a-96af-4531-bce0-ceecf13a2ae3",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "4a03d09b-e893-46bb-a047-65e2a63ddd49",
            "trainingSessionID": "4302a161-5be7-4d78-8a5e-723893b203eb",
            "movementBaseID": "base-2",
            "movementBase": {
              "movementBaseID": "base-2",
              "name": "Exercise 2"
            },
            "movementModifierID": "mod-2",
            "movementModifier": {
              "movementModifierID": "mod-2",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "1633eaee-c8c6-49f5-8356-76a878259c1f",
                "movementID": "4a03d09b-e893-46bb-a047-65e2a63ddd49",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "9bbd6c8b-8689-40b9-a5f3-f7037f4bbcd9",
                "movementID": "4a03d09b-e893-46bb-a047-65e2a63ddd49",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "af4d7f93-47b9-428d-b2d5-159289d30007",
                "movementID": "4a03d09b-e893-46bb-a047-65e2a63ddd49",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "df523617-8f47-482b-9a48-59b1591b9db2",
            "trainingSessionID": "4302a161-5be7-4d78-8a5e-723893b203eb",
            "movementBaseID": "base-3",
            "movementBase": {
              "movementBaseID": "base-3",
              "name": "Exercise 3"
            },
            "movementModifierID": "mod-3",
            "movementModifier": {
              "movementModifierID": "mod-3",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "4b4aa17b-0e82-4961-ae3f-96c2ed2448d2",
                "movementID": "df523617-8f47-482b-9a48-59b1591b9db2",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "df3f4d30-9a4a-4955-9a0c-4e5c7629915a",
                "movementID": "df523617-8f47-482b-9a48-59b1591b9db2",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "e3277614-8179-4ccb-91cb-38810d29c58c",
                "movementID": "df523617-8f47-482b-9a48-59b1591b9db2",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "860fd28d-0ccc-457b-b2a8-8cbd4cefeb3a",
            "trainingSessionID": "4302a161-5be7-4d78-8a5e-723893b203eb",
            "movementBaseID": "base-4",
            "movementBase": {
              "movementBaseID": "base-4",
              "name": "Exercise 4"
            },
            "movementModifierID": "mod-4",
            "movementModifier": {
              "movementModifierID": "mod-4",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "9222a46c-9ebf-40e3-893d-303203d1d7f3",
                "movementID": "860fd28d-0ccc-457b-b2a8-8cbd4cefeb3a",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "7488d43d-3b5b-4c79-ba21-e0b9d71a46ad",
                "movementID": "860fd28d-0ccc-457b-b2a8-8cbd4cefeb3a",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "7411c80b-3635-414e-9f49-92c007f1597d",
                "movementID": "860fd28d-0ccc-457b-b2a8-8cbd4cefeb3a",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "4e4c8557-0033-427a-98c6-b83aaa9639a4",
            "trainingSessionID": "4302a161-5be7-4d78-8a5e-723893b203eb",
            "movementBaseID": "base-5",
            "movementBase": {
              "movementBaseID": "base-5",
              "name": "Exercise 5"
            },
            "movementModifierID": "mod-5",
            "movementModifier": {
              "movementModifierID": "mod-5",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "0e40dc54-4eb8-4953-bbd2-d65090d356df",
                "movementID": "4e4c8557-0033-427a-98c6-b83aaa9639a4",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "d476c655-11bb-4f35-a48e-5e6f279b1ced",
                "movementID": "4e4c8557-0033-427a-98c6-b83aaa9639a4",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "f5b47c4a-210d-443b-b1dc-c16050de60d5",
                "movementID": "4e4c8557-0033-427a-98c6-b83aaa9639a4",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          }
        ]
      },
      {
        "sessionID": "acc76945-30d2-4a30-80b4-edd1ae639cf5",
        "programID": "a4311237-3f49-42d7-a4b5-51bbb31fb62e",
        "date": "2025-06-03",
        "movements": [
          {
            "movementID": "e1c22629-a83b-4a3f-9a92-55ddcfad8bf4",
            "trainingSessionID": "acc76945-30d2-4a30-80b4-edd1ae639cf5",
            "movementBaseID": "base-1",
            "movementBase": {
              "movementBaseID": "base-1",
              "name": "Exercise 1"
            },
            "movementModifierID": "mod-1",
            "movementModifier": {
              "movementModifierID": "mod-1",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "44d380a5-a3da-4971-baab-50d3b6f6e2ef",
                "movementID": "e1c22629-a83b-4a3f-9a92-55ddcfad8bf4",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "c6e68bb7-5394-4e7f-bc2c-68d091d528b8",
                "movementID": "e1c22629-a83b-4a3f-9a92-55ddcfad8bf4",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "c318e993-139b-4225-9877-867f3e2bd01d",
                "movementID": "e1c22629-a83b-4a3f-9a92-55ddcfad8bf4",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "e6c4bb7a-201a-49db-b5be-be8ad457350b",
            "trainingSessionID": "acc76945-30d2-4a30-80b4-edd1ae639cf5",
            "movementBaseID": "base-2",
            "movementBase": {
              "movementBaseID": "base-2",
              "name": "Exercise 2"
            },
            "movementModifierID": "mod-2",
            "movementModifier": {
              "movementModifierID": "mod-2",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "d4af6a33-e0e9-46fc-a9c6-d4eb62e9c04c",
                "movementID": "e6c4bb7a-201a-49db-b5be-be8ad457350b",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "1098e408-0da1-4a6e-a0af-2f11c4672dd8",
                "movementID": "e6c4bb7a-201a-49db-b5be-be8ad457350b",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "4638be65-9c7c-4216-8363-7a43107bdb96",
                "movementID": "e6c4bb7a-201a-49db-b5be-be8ad457350b",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "bc130660-be1c-4827-9baf-b084e52f3fd5",
            "trainingSessionID": "acc76945-30d2-4a30-80b4-edd1ae639cf5",
            "movementBaseID": "base-3",
            "movementBase": {
              "movementBaseID": "base-3",
              "name": "Exercise 3"
            },
            "movementModifierID": "mod-3",
            "movementModifier": {
              "movementModifierID": "mod-3",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "5e50777a-3a39-4d02-bb3a-28a35a9d3f86",
                "movementID": "bc130660-be1c-4827-9baf-b084e52f3fd5",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "e358c27b-4b43-4900-9482-f9ed2fcd0d14",
                "movementID": "bc130660-be1c-4827-9baf-b084e52f3fd5",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "bf39515c-4601-4bf0-8c9f-01ef9250325d",
                "movementID": "bc130660-be1c-4827-9baf-b084e52f3fd5",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "8474375f-a044-49fd-b700-96cd3faedd19",
            "trainingSessionID": "acc76945-30d2-4a30-80b4-edd1ae639cf5",
            "movementBaseID": "base-4",
            "movementBase": {
              "movementBaseID": "base-4",
              "name": "Exercise 4"
            },
            "movementModifierID": "mod-4",
            "movementModifier": {
              "movementModifierID": "mod-4",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "0fabab5e-be8a-480e-ac1b-b69403f92692",
                "movementID": "8474375f-a044-49fd-b700-96cd3faedd19",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "0fe69084-1d8d-411d-a6ce-793c1d2abd4c",
                "movementID": "8474375f-a044-49fd-b700-96cd3faedd19",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "669484c0-c22e-4ed2-8556-42ee37dacaef",
                "movementID": "8474375f-a044-49fd-b700-96cd3faedd19",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "a2fa6c09-ba36-4a61-82ad-4386fcd807ef",
            "trainingSessionID": "acc76945-30d2-4a30-80b4-edd1ae639cf5",
            "movementBaseID": "base-5",
            "movementBase": {
              "movementBaseID": "base-5",
              "name": "Exercise 5"
            },
            "movementModifierID": "mod-5",
            "movementModifier": {
              "movementModifierID": "mod-5",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "89e72086-4e7e-489f-a831-d19f4223f0d8",
                "movementID": "a2fa6c09-ba36-4a61-82ad-4386fcd807ef",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "f76c475b-a3de-4140-a1e2-3215d282e17f",
                "movementID": "a2fa6c09-ba36-4a61-82ad-4386fcd807ef",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "22b3e135-2819-4ed1-9204-656675eab5f2",
                "movementID": "a2fa6c09-ba36-4a61-82ad-4386fcd807ef",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          }
        ]
      },
      {
        "sessionID": "0c598976-0997-4339-94e2-03c27c9de342",
        "programID": "a4311237-3f49-42d7-a4b5-51bbb31fb62e",
        "date": "2025-06-05",
        "movements": [
          {
            "movementID": "579e98ab-eaa5-46bf-b72d-42ab656846de",
            "trainingSessionID": "0c598976-0997-4339-94e2-03c27c9de342",
            "movementBaseID": "base-1",
            "movementBase": {
              "movementBaseID": "base-1",
              "name": "Exercise 1"
            },
            "movementModifierID": "mod-1",
            "movementModifier": {
              "movementModifierID": "mod-1",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "28d12de5-02de-453c-b3bb-37a82a1e01dd",
                "movementID": "579e98ab-eaa5-46bf-b72d-42ab656846de",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "cff12d1f-a33d-487e-8389-4c740cd7a948",
                "movementID": "579e98ab-eaa5-46bf-b72d-42ab656846de",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "b7e85633-3160-4186-afd2-0b3f8eed9a5f",
                "movementID": "579e98ab-eaa5-46bf-b72d-42ab656846de",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "fb05c79c-d8b4-4eb5-8843-4741a34e17c2",
            "trainingSessionID": "0c598976-0997-4339-94e2-03c27c9de342",
            "movementBaseID": "base-2",
            "movementBase": {
              "movementBaseID": "base-2",
              "name": "Exercise 2"
            },
            "movementModifierID": "mod-2",
            "movementModifier": {
              "movementModifierID": "mod-2",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "6e8b63f3-f8c2-4c23-8cdb-ddf42872ac5f",
                "movementID": "fb05c79c-d8b4-4eb5-8843-4741a34e17c2",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "761f9b55-9b21-4d51-b47a-02883eb8a6a0",
                "movementID": "fb05c79c-d8b4-4eb5-8843-4741a34e17c2",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "77e03774-50b8-4880-b595-ba8b7c422672",
                "movementID": "fb05c79c-d8b4-4eb5-8843-4741a34e17c2",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "781c0be7-eb2c-4854-a4e6-5dcfa8dbfaaa",
            "trainingSessionID": "0c598976-0997-4339-94e2-03c27c9de342",
            "movementBaseID": "base-3",
            "movementBase": {
              "movementBaseID": "base-3",
              "name": "Exercise 3"
            },
            "movementModifierID": "mod-3",
            "movementModifier": {
              "movementModifierID": "mod-3",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "03218c32-fa9a-4d50-a4a5-c5fdae2de883",
                "movementID": "781c0be7-eb2c-4854-a4e6-5dcfa8dbfaaa",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "6fce16f9-fa2a-4b21-a2f1-7d7e7596fd11",
                "movementID": "781c0be7-eb2c-4854-a4e6-5dcfa8dbfaaa",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "571b5ef9-988d-4fe0-a209-ece6a7e347d0",
                "movementID": "781c0be7-eb2c-4854-a4e6-5dcfa8dbfaaa",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "25659156-192b-4ef2-ab19-722410f2fdf3",
            "trainingSessionID": "0c598976-0997-4339-94e2-03c27c9de342",
            "movementBaseID": "base-4",
            "movementBase": {
              "movementBaseID": "base-4",
              "name": "Exercise 4"
            },
            "movementModifierID": "mod-4",
            "movementModifier": {
              "movementModifierID": "mod-4",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "8c0b3f42-f89e-47e0-9e18-0de4f7a37435",
                "movementID": "25659156-192b-4ef2-ab19-722410f2fdf3",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "49236a4b-5281-42b3-922e-59178c3f0429",
                "movementID": "25659156-192b-4ef2-ab19-722410f2fdf3",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "f96f115a-c579-467e-b046-d42c844267cc",
                "movementID": "25659156-192b-4ef2-ab19-722410f2fdf3",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "c5b8aeee-f43a-4c8a-a829-086b1f9e9e5f",
            "trainingSessionID": "0c598976-0997-4339-94e2-03c27c9de342",
            "movementBaseID": "base-5",
            "movementBase": {
              "movementBaseID": "base-5",
              "name": "Exercise 5"
            },
            "movementModifierID": "mod-5",
            "movementModifier": {
              "movementModifierID": "mod-5",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "f4029e9d-4d88-4ee8-8357-db0f14e9f6df",
                "movementID": "c5b8aeee-f43a-4c8a-a829-086b1f9e9e5f",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "c69e7924-754f-48cc-bb15-eeae676cbe26",
                "movementID": "c5b8aeee-f43a-4c8a-a829-086b1f9e9e5f",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "54c25850-030f-44c3-b86f-cd82221f1135",
                "movementID": "c5b8aeee-f43a-4c8a-a829-086b1f9e9e5f",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          }
        ]
      },
      {
        "sessionID": "5c4dd516-bfe9-4272-9fb2-6fb15037c2b9",
        "programID": "a4311237-3f49-42d7-a4b5-51bbb31fb62e",
        "date": "2025-06-07",
        "movements": [
          {
            "movementID": "2a96a87d-3edb-4692-be7d-55ed300c390f",
            "trainingSessionID": "5c4dd516-bfe9-4272-9fb2-6fb15037c2b9",
            "movementBaseID": "base-1",
            "movementBase": {
              "movementBaseID": "base-1",
              "name": "Exercise 1"
            },
            "movementModifierID": "mod-1",
            "movementModifier": {
              "movementModifierID": "mod-1",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "2089fe06-ff52-48f5-9420-92ef68749e09",
                "movementID": "2a96a87d-3edb-4692-be7d-55ed300c390f",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "11d7bbea-5815-4ed2-8bcd-8ff76613347c",
                "movementID": "2a96a87d-3edb-4692-be7d-55ed300c390f",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "79cceb35-91bf-45b9-896f-66e18d3c8b31",
                "movementID": "2a96a87d-3edb-4692-be7d-55ed300c390f",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "728798fd-67f9-4bef-8ba5-dfce68f4d55a",
            "trainingSessionID": "5c4dd516-bfe9-4272-9fb2-6fb15037c2b9",
            "movementBaseID": "base-2",
            "movementBase": {
              "movementBaseID": "base-2",
              "name": "Exercise 2"
            },
            "movementModifierID": "mod-2",
            "movementModifier": {
              "movementModifierID": "mod-2",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "24001658-284d-4911-acb7-02501d789665",
                "movementID": "728798fd-67f9-4bef-8ba5-dfce68f4d55a",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "1204514d-b54a-468f-b99b-768ef30999a2",
                "movementID": "728798fd-67f9-4bef-8ba5-dfce68f4d55a",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "b6b684e6-3fb7-49d3-a639-c747d51b544e",
                "movementID": "728798fd-67f9-4bef-8ba5-dfce68f4d55a",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "90e12bf7-f1cb-40c9-8243-42468eeb482d",
            "trainingSessionID": "5c4dd516-bfe9-4272-9fb2-6fb15037c2b9",
            "movementBaseID": "base-3",
            "movementBase": {
              "movementBaseID": "base-3",
              "name": "Exercise 3"
            },
            "movementModifierID": "mod-3",
            "movementModifier": {
              "movementModifierID": "mod-3",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "4c2183c0-1927-4ee7-ae9c-4a98ac80b1c9",
                "movementID": "90e12bf7-f1cb-40c9-8243-42468eeb482d",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "a508cd3d-2dd3-465f-928b-e51a087895c1",
                "movementID": "90e12bf7-f1cb-40c9-8243-42468eeb482d",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "d6a7c4b1-4d4a-490c-ae94-306b0c702162",
                "movementID": "90e12bf7-f1cb-40c9-8243-42468eeb482d",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "de996e80-f43c-472a-9e43-5823addfe334",
            "trainingSessionID": "5c4dd516-bfe9-4272-9fb2-6fb15037c2b9",
            "movementBaseID": "base-4",
            "movementBase": {
              "movementBaseID": "base-4",
              "name": "Exercise 4"
            },
            "movementModifierID": "mod-4",
            "movementModifier": {
              "movementModifierID": "mod-4",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "f6f36537-ae53-4d2a-8b67-fa5fd17d940a",
                "movementID": "de996e80-f43c-472a-9e43-5823addfe334",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "23ec0d5f-655b-489a-b234-5f9b4453e44d",
                "movementID": "de996e80-f43c-472a-9e43-5823addfe334",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "8791fb8a-82c1-4f3f-858b-78a08f2f9388",
                "movementID": "de996e80-f43c-472a-9e43-5823addfe334",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "d6076ec2-d3df-458f-b148-82babdd36db6",
            "trainingSessionID": "5c4dd516-bfe9-4272-9fb2-6fb15037c2b9",
            "movementBaseID": "base-5",
            "movementBase": {
              "movementBaseID": "base-5",
              "name": "Exercise 5"
            },
            "movementModifierID": "mod-5",
            "movementModifier": {
              "movementModifierID": "mod-5",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "05a787ea-6b77-4ef0-b450-766fb1332678",
                "movementID": "d6076ec2-d3df-458f-b148-82babdd36db6",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "f13f1081-94f3-4893-bd43-0efd391abb01",
                "movementID": "d6076ec2-d3df-458f-b148-82babdd36db6",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "8b6cdb6e-0b0b-45a8-8a23-4d52b8706c26",
                "movementID": "d6076ec2-d3df-458f-b148-82babdd36db6",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          }
        ]
      },
      {
        "sessionID": "0c54fc21-94b8-4c5e-8a79-ff4da5b5584f",
        "programID": "a4311237-3f49-42d7-a4b5-51bbb31fb62e",
        "date": "2025-06-09",
        "movements": [
          {
            "movementID": "99cec8bb-ada3-4027-b85c-04d60765e98a",
            "trainingSessionID": "0c54fc21-94b8-4c5e-8a79-ff4da5b5584f",
            "movementBaseID": "base-1",
            "movementBase": {
              "movementBaseID": "base-1",
              "name": "Exercise 1"
            },
            "movementModifierID": "mod-1",
            "movementModifier": {
              "movementModifierID": "mod-1",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "14ac539d-4cfe-4e79-a2ee-c59d97dfaa8f",
                "movementID": "99cec8bb-ada3-4027-b85c-04d60765e98a",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "4a16c360-da78-4db6-8299-66d3bd8959a4",
                "movementID": "99cec8bb-ada3-4027-b85c-04d60765e98a",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "17ef3828-3dd8-4fa1-9444-b09d6acd8e2c",
                "movementID": "99cec8bb-ada3-4027-b85c-04d60765e98a",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "d136351e-06b9-4916-9deb-7bb08085d861",
            "trainingSessionID": "0c54fc21-94b8-4c5e-8a79-ff4da5b5584f",
            "movementBaseID": "base-2",
            "movementBase": {
              "movementBaseID": "base-2",
              "name": "Exercise 2"
            },
            "movementModifierID": "mod-2",
            "movementModifier": {
              "movementModifierID": "mod-2",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "19af3601-ae97-4c8f-85a0-9672b4208d5a",
                "movementID": "d136351e-06b9-4916-9deb-7bb08085d861",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "3f52c9b9-349f-454d-b7f9-92d3621dd805",
                "movementID": "d136351e-06b9-4916-9deb-7bb08085d861",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "6f07b0e1-c857-4fcb-8fd9-e28b4b053161",
                "movementID": "d136351e-06b9-4916-9deb-7bb08085d861",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "a4c97c7d-6631-4189-b3e6-dff64a63ca42",
            "trainingSessionID": "0c54fc21-94b8-4c5e-8a79-ff4da5b5584f",
            "movementBaseID": "base-3",
            "movementBase": {
              "movementBaseID": "base-3",
              "name": "Exercise 3"
            },
            "movementModifierID": "mod-3",
            "movementModifier": {
              "movementModifierID": "mod-3",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "f0938820-a085-4171-b9da-4ff31b55897c",
                "movementID": "a4c97c7d-6631-4189-b3e6-dff64a63ca42",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "0db61b47-e2ac-413b-a276-d612b673a4d1",
                "movementID": "a4c97c7d-6631-4189-b3e6-dff64a63ca42",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "36b6f907-bd9d-4957-863a-a581eded2c80",
                "movementID": "a4c97c7d-6631-4189-b3e6-dff64a63ca42",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "14dd4fb5-b7da-414f-bd9b-e6cdd67e3238",
            "trainingSessionID": "0c54fc21-94b8-4c5e-8a79-ff4da5b5584f",
            "movementBaseID": "base-4",
            "movementBase": {
              "movementBaseID": "base-4",
              "name": "Exercise 4"
            },
            "movementModifierID": "mod-4",
            "movementModifier": {
              "movementModifierID": "mod-4",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "08c8c2be-971c-4d58-bd19-a59cab485846",
                "movementID": "14dd4fb5-b7da-414f-bd9b-e6cdd67e3238",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "321c2b38-d938-4667-af26-d8db93e10fb5",
                "movementID": "14dd4fb5-b7da-414f-bd9b-e6cdd67e3238",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "a67c37ef-f9a1-42ca-ad30-e39b9f1914fb",
                "movementID": "14dd4fb5-b7da-414f-bd9b-e6cdd67e3238",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "e7b266a0-3722-4271-b9ea-f4bb290ed036",
            "trainingSessionID": "0c54fc21-94b8-4c5e-8a79-ff4da5b5584f",
            "movementBaseID": "base-5",
            "movementBase": {
              "movementBaseID": "base-5",
              "name": "Exercise 5"
            },
            "movementModifierID": "mod-5",
            "movementModifier": {
              "movementModifierID": "mod-5",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "e8f99353-19e0-4fde-9092-09a6ec5e48a8",
                "movementID": "e7b266a0-3722-4271-b9ea-f4bb290ed036",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "60cedf78-41c4-43d3-8da3-dd5e2501a74f",
                "movementID": "e7b266a0-3722-4271-b9ea-f4bb290ed036",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "58eeed96-3116-47e9-85b9-3fa25896caef",
                "movementID": "e7b266a0-3722-4271-b9ea-f4bb290ed036",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          }
        ]
      },
      {
        "sessionID": "99e654b4-8af3-4c10-866b-526b0b4eadd4",
        "programID": "a4311237-3f49-42d7-a4b5-51bbb31fb62e",
        "date": "2025-06-11",
        "movements": [
          {
            "movementID": "12fee12e-8cc9-4e29-ba36-c1426bc8b5d6",
            "trainingSessionID": "99e654b4-8af3-4c10-866b-526b0b4eadd4",
            "movementBaseID": "base-1",
            "movementBase": {
              "movementBaseID": "base-1",
              "name": "Exercise 1"
            },
            "movementModifierID": "mod-1",
            "movementModifier": {
              "movementModifierID": "mod-1",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "9bfa14c3-4ad5-468c-b707-16821b96fc42",
                "movementID": "12fee12e-8cc9-4e29-ba36-c1426bc8b5d6",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "8c0843d6-8b0c-4d28-b3c8-853d898caf69",
                "movementID": "12fee12e-8cc9-4e29-ba36-c1426bc8b5d6",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "eb2749e3-55a5-4cd2-94c8-c96fca85091e",
                "movementID": "12fee12e-8cc9-4e29-ba36-c1426bc8b5d6",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "9cb3d270-8e1d-472e-ae86-3b1d2c9dc58c",
            "trainingSessionID": "99e654b4-8af3-4c10-866b-526b0b4eadd4",
            "movementBaseID": "base-2",
            "movementBase": {
              "movementBaseID": "base-2",
              "name": "Exercise 2"
            },
            "movementModifierID": "mod-2",
            "movementModifier": {
              "movementModifierID": "mod-2",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "96291c83-a464-4e5f-8865-2c25798d79f1",
                "movementID": "9cb3d270-8e1d-472e-ae86-3b1d2c9dc58c",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "41e2a246-4851-42a2-ae5a-7d0b8af47a9a",
                "movementID": "9cb3d270-8e1d-472e-ae86-3b1d2c9dc58c",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "c299505a-008b-40d1-a691-4e833b1d8527",
                "movementID": "9cb3d270-8e1d-472e-ae86-3b1d2c9dc58c",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "7273c318-797c-412a-abff-feb17c20f5a6",
            "trainingSessionID": "99e654b4-8af3-4c10-866b-526b0b4eadd4",
            "movementBaseID": "base-3",
            "movementBase": {
              "movementBaseID": "base-3",
              "name": "Exercise 3"
            },
            "movementModifierID": "mod-3",
            "movementModifier": {
              "movementModifierID": "mod-3",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "1fc12765-2469-40a6-af4c-1e940686f011",
                "movementID": "7273c318-797c-412a-abff-feb17c20f5a6",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "c4dd50da-55f0-4496-9d06-5960d39676c3",
                "movementID": "7273c318-797c-412a-abff-feb17c20f5a6",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "116f85b2-c62e-485f-9eed-42763d2a4ba4",
                "movementID": "7273c318-797c-412a-abff-feb17c20f5a6",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "23671e6d-274a-4950-9c7e-fa556b3b04f6",
            "trainingSessionID": "99e654b4-8af3-4c10-866b-526b0b4eadd4",
            "movementBaseID": "base-4",
            "movementBase": {
              "movementBaseID": "base-4",
              "name": "Exercise 4"
            },
            "movementModifierID": "mod-4",
            "movementModifier": {
              "movementModifierID": "mod-4",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "c5ab9b60-db35-4d65-a30a-edc7aaab86b7",
                "movementID": "23671e6d-274a-4950-9c7e-fa556b3b04f6",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "daaf5a50-789f-4878-9c94-f4e74a083f41",
                "movementID": "23671e6d-274a-4950-9c7e-fa556b3b04f6",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "13a3a9ca-d92f-4e02-8ea7-cc64b92a83e0",
                "movementID": "23671e6d-274a-4950-9c7e-fa556b3b04f6",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "753138b7-1d83-4f80-803a-2b15ffee0fc9",
            "trainingSessionID": "99e654b4-8af3-4c10-866b-526b0b4eadd4",
            "movementBaseID": "base-5",
            "movementBase": {
              "movementBaseID": "base-5",
              "name": "Exercise 5"
            },
            "movementModifierID": "mod-5",
            "movementModifier": {
              "movementModifierID": "mod-5",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "a535b9ed-44ea-4441-8aba-19c23601c93c",
                "movementID": "753138b7-1d83-4f80-803a-2b15ffee0fc9",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "87d8895b-152a-4b19-8f1b-f8dd26651cc5",
                "movementID": "753138b7-1d83-4f80-803a-2b15ffee0fc9",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "9e49f798-1aa3-470c-b019-3c4e9e9cc9f2",
                "movementID": "753138b7-1d83-4f80-803a-2b15ffee0fc9",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          }
        ]
      },
      {
        "sessionID": "56b5bc11-d702-4b77-966d-02cd9cf6ba8a",
        "programID": "a4311237-3f49-42d7-a4b5-51bbb31fb62e",
        "date": "2025-06-13",
        "movements": [
          {
            "movementID": "3ba1c0de-ce34-4b0c-87b2-9cec3a4107d4",
            "trainingSessionID": "56b5bc11-d702-4b77-966d-02cd9cf6ba8a",
            "movementBaseID": "base-1",
            "movementBase": {
              "movementBaseID": "base-1",
              "name": "Exercise 1"
            },
            "movementModifierID": "mod-1",
            "movementModifier": {
              "movementModifierID": "mod-1",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "c6f9e1af-b425-4571-8af7-850c872f4a29",
                "movementID": "3ba1c0de-ce34-4b0c-87b2-9cec3a4107d4",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "c8bf1e86-7aec-4fa0-8bd4-cd314c91f42d",
                "movementID": "3ba1c0de-ce34-4b0c-87b2-9cec3a4107d4",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "37575068-673f-434a-ad34-2605503e0879",
                "movementID": "3ba1c0de-ce34-4b0c-87b2-9cec3a4107d4",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "2f06c162-4438-4dd0-9dbb-896ad92bbe80",
            "trainingSessionID": "56b5bc11-d702-4b77-966d-02cd9cf6ba8a",
            "movementBaseID": "base-2",
            "movementBase": {
              "movementBaseID": "base-2",
              "name": "Exercise 2"
            },
            "movementModifierID": "mod-2",
            "movementModifier": {
              "movementModifierID": "mod-2",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "c2ba864a-fa3f-4ecf-a61b-b327f123337d",
                "movementID": "2f06c162-4438-4dd0-9dbb-896ad92bbe80",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "79d8fd5e-0a6d-4b87-8a5b-0df997643e25",
                "movementID": "2f06c162-4438-4dd0-9dbb-896ad92bbe80",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "f95d895d-6a6a-4925-8649-a725ec008a90",
                "movementID": "2f06c162-4438-4dd0-9dbb-896ad92bbe80",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "9d6e0658-f5e8-4a47-962d-1439d4d80e1f",
            "trainingSessionID": "56b5bc11-d702-4b77-966d-02cd9cf6ba8a",
            "movementBaseID": "base-3",
            "movementBase": {
              "movementBaseID": "base-3",
              "name": "Exercise 3"
            },
            "movementModifierID": "mod-3",
            "movementModifier": {
              "movementModifierID": "mod-3",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "e87a10ca-9db7-43d7-a3ae-a06577096ff3",
                "movementID": "9d6e0658-f5e8-4a47-962d-1439d4d80e1f",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "65a0b625-678e-49b3-b12d-eaeca94a0276",
                "movementID": "9d6e0658-f5e8-4a47-962d-1439d4d80e1f",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "9ebb63d5-c847-47fc-ada3-9e9c6d8ea975",
                "movementID": "9d6e0658-f5e8-4a47-962d-1439d4d80e1f",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "9800a7f4-653d-4774-abaf-3bbff9b7f2f2",
            "trainingSessionID": "56b5bc11-d702-4b77-966d-02cd9cf6ba8a",
            "movementBaseID": "base-4",
            "movementBase": {
              "movementBaseID": "base-4",
              "name": "Exercise 4"
            },
            "movementModifierID": "mod-4",
            "movementModifier": {
              "movementModifierID": "mod-4",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "cb08c8e7-2a66-4a74-b480-8bef3351d168",
                "movementID": "9800a7f4-653d-4774-abaf-3bbff9b7f2f2",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "32785efb-ad52-40ad-a2d5-9bf58a14e8f0",
                "movementID": "9800a7f4-653d-4774-abaf-3bbff9b7f2f2",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "f2691f47-9e4d-4c2e-a5f4-f2a8d5cdaed7",
                "movementID": "9800a7f4-653d-4774-abaf-3bbff9b7f2f2",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "d800f82e-43a9-478f-aefe-89577db4a23e",
            "trainingSessionID": "56b5bc11-d702-4b77-966d-02cd9cf6ba8a",
            "movementBaseID": "base-5",
            "movementBase": {
              "movementBaseID": "base-5",
              "name": "Exercise 5"
            },
            "movementModifierID": "mod-5",
            "movementModifier": {
              "movementModifierID": "mod-5",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "127766d9-bf60-450b-8047-e408ed17904a",
                "movementID": "d800f82e-43a9-478f-aefe-89577db4a23e",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "8bc8ec8c-aa64-460e-88b7-34c4899f53ab",
                "movementID": "d800f82e-43a9-478f-aefe-89577db4a23e",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "21caad82-db5a-4cb4-9842-9e87a0ee4a45",
                "movementID": "d800f82e-43a9-478f-aefe-89577db4a23e",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          }
        ]
      },
      {
        "sessionID": "c8b82169-3983-48d6-8b83-80996d6fa130",
        "programID": "a4311237-3f49-42d7-a4b5-51bbb31fb62e",
        "date": "2025-06-15",
        "movements": [
          {
            "movementID": "e516a4f4-fb37-427c-ba9e-408042306acc",
            "trainingSessionID": "c8b82169-3983-48d6-8b83-80996d6fa130",
            "movementBaseID": "base-1",
            "movementBase": {
              "movementBaseID": "base-1",
              "name": "Exercise 1"
            },
            "movementModifierID": "mod-1",
            "movementModifier": {
              "movementModifierID": "mod-1",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "b4dfb521-d9d4-4b0f-801d-0a2d1f99ca02",
                "movementID": "e516a4f4-fb37-427c-ba9e-408042306acc",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "579d2e52-5dd8-4820-9757-acd4dfd503a5",
                "movementID": "e516a4f4-fb37-427c-ba9e-408042306acc",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "957f2158-659e-48c5-a4f5-0271d5938618",
                "movementID": "e516a4f4-fb37-427c-ba9e-408042306acc",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "c225451b-4b45-4d2b-8344-441126cb409c",
            "trainingSessionID": "c8b82169-3983-48d6-8b83-80996d6fa130",
            "movementBaseID": "base-2",
            "movementBase": {
              "movementBaseID": "base-2",
              "name": "Exercise 2"
            },
            "movementModifierID": "mod-2",
            "movementModifier": {
              "movementModifierID": "mod-2",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "bc8586de-abc3-4861-9c2d-b092d42bc4ea",
                "movementID": "c225451b-4b45-4d2b-8344-441126cb409c",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "e31237f4-f445-4e05-b5e4-16bb41a79657",
                "movementID": "c225451b-4b45-4d2b-8344-441126cb409c",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "5f38d63c-2359-46ac-a242-c79985d8ad9b",
                "movementID": "c225451b-4b45-4d2b-8344-441126cb409c",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "54d49b1d-340a-46bf-9072-ac961e2cbc79",
            "trainingSessionID": "c8b82169-3983-48d6-8b83-80996d6fa130",
            "movementBaseID": "base-3",
            "movementBase": {
              "movementBaseID": "base-3",
              "name": "Exercise 3"
            },
            "movementModifierID": "mod-3",
            "movementModifier": {
              "movementModifierID": "mod-3",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "055af333-ebe6-48fe-8c91-7c1aae8c2e9f",
                "movementID": "54d49b1d-340a-46bf-9072-ac961e2cbc79",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "3e7f62de-c40a-471a-b6b0-6ae85e079637",
                "movementID": "54d49b1d-340a-46bf-9072-ac961e2cbc79",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "f842cc43-ef22-440b-8c97-d36e7d16680f",
                "movementID": "54d49b1d-340a-46bf-9072-ac961e2cbc79",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "2a951fb2-7cc2-4497-b017-8b36c2b6713b",
            "trainingSessionID": "c8b82169-3983-48d6-8b83-80996d6fa130",
            "movementBaseID": "base-4",
            "movementBase": {
              "movementBaseID": "base-4",
              "name": "Exercise 4"
            },
            "movementModifierID": "mod-4",
            "movementModifier": {
              "movementModifierID": "mod-4",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "b9eeec4a-70bd-4432-ab7c-0e2f02f731cf",
                "movementID": "2a951fb2-7cc2-4497-b017-8b36c2b6713b",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "85c20b26-4405-45ab-8c94-e110f448ae91",
                "movementID": "2a951fb2-7cc2-4497-b017-8b36c2b6713b",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "9fc463a4-7817-4412-b87e-443a0546c48a",
                "movementID": "2a951fb2-7cc2-4497-b017-8b36c2b6713b",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "e48cada7-4a89-43d1-a1be-3307b9bfe3a1",
            "trainingSessionID": "c8b82169-3983-48d6-8b83-80996d6fa130",
            "movementBaseID": "base-5",
            "movementBase": {
              "movementBaseID": "base-5",
              "name": "Exercise 5"
            },
            "movementModifierID": "mod-5",
            "movementModifier": {
              "movementModifierID": "mod-5",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "e153cf17-0621-499f-82c5-dd6d1dd274a1",
                "movementID": "e48cada7-4a89-43d1-a1be-3307b9bfe3a1",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "0b070a81-ae92-4eea-83e0-ee241fcb12ec",
                "movementID": "e48cada7-4a89-43d1-a1be-3307b9bfe3a1",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "13930c3e-38dc-47fb-903f-e713160d2ed0",
                "movementID": "e48cada7-4a89-43d1-a1be-3307b9bfe3a1",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          }
        ]
      },
      {
        "sessionID": "620fdc28-a546-4ff0-96c5-8226dbd2e9aa",
        "programID": "a4311237-3f49-42d7-a4b5-51bbb31fb62e",
        "date": "2025-06-17",
        "movements": [
          {
            "movementID": "69488100-c1e0-423e-a99a-8c3f0378f944",
            "trainingSessionID": "620fdc28-a546-4ff0-96c5-8226dbd2e9aa",
            "movementBaseID": "base-1",
            "movementBase": {
              "movementBaseID": "base-1",
              "name": "Exercise 1"
            },
            "movementModifierID": "mod-1",
            "movementModifier": {
              "movementModifierID": "mod-1",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "cda83f71-6406-4fdc-af3b-01324dfcb5c4",
                "movementID": "69488100-c1e0-423e-a99a-8c3f0378f944",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "8d8a051c-4cc9-4310-b769-bbc1ad84521b",
                "movementID": "69488100-c1e0-423e-a99a-8c3f0378f944",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "5da9c772-803c-466d-b31b-d5cf97f9f16d",
                "movementID": "69488100-c1e0-423e-a99a-8c3f0378f944",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "83a4d7e4-fd6b-40ce-b89a-080d07215f59",
            "trainingSessionID": "620fdc28-a546-4ff0-96c5-8226dbd2e9aa",
            "movementBaseID": "base-2",
            "movementBase": {
              "movementBaseID": "base-2",
              "name": "Exercise 2"
            },
            "movementModifierID": "mod-2",
            "movementModifier": {
              "movementModifierID": "mod-2",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "f1e9a809-a36e-4b7a-9b5b-6dbc1b071b91",
                "movementID": "83a4d7e4-fd6b-40ce-b89a-080d07215f59",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "8018dbb7-fa8a-4893-bf44-e242dc8ef751",
                "movementID": "83a4d7e4-fd6b-40ce-b89a-080d07215f59",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "a07a7a32-da66-49e1-89b9-fa37475db66d",
                "movementID": "83a4d7e4-fd6b-40ce-b89a-080d07215f59",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "22ff26e1-34b9-46a7-bd77-0710dbacb31f",
            "trainingSessionID": "620fdc28-a546-4ff0-96c5-8226dbd2e9aa",
            "movementBaseID": "base-3",
            "movementBase": {
              "movementBaseID": "base-3",
              "name": "Exercise 3"
            },
            "movementModifierID": "mod-3",
            "movementModifier": {
              "movementModifierID": "mod-3",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "0ceb5aed-aecc-41f8-b245-c8655f0554c1",
                "movementID": "22ff26e1-34b9-46a7-bd77-0710dbacb31f",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "25c99087-f93b-4ad9-8942-5e7c3137a5be",
                "movementID": "22ff26e1-34b9-46a7-bd77-0710dbacb31f",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "50987212-f86a-49f8-ad16-6b1e0e2ef916",
                "movementID": "22ff26e1-34b9-46a7-bd77-0710dbacb31f",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "dc990a5c-63a9-412e-b036-3d22fe58f1b2",
            "trainingSessionID": "620fdc28-a546-4ff0-96c5-8226dbd2e9aa",
            "movementBaseID": "base-4",
            "movementBase": {
              "movementBaseID": "base-4",
              "name": "Exercise 4"
            },
            "movementModifierID": "mod-4",
            "movementModifier": {
              "movementModifierID": "mod-4",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "25586db7-1c31-4d34-bd50-a878e04e1766",
                "movementID": "dc990a5c-63a9-412e-b036-3d22fe58f1b2",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "d369abbf-2c52-4c10-90ab-8ea3ece3af2f",
                "movementID": "dc990a5c-63a9-412e-b036-3d22fe58f1b2",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "a7c7df73-0e0e-4842-b1a0-67f085b7832a",
                "movementID": "dc990a5c-63a9-412e-b036-3d22fe58f1b2",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "a178bf46-d56a-467d-9e8e-8ad9d2a978d2",
            "trainingSessionID": "620fdc28-a546-4ff0-96c5-8226dbd2e9aa",
            "movementBaseID": "base-5",
            "movementBase": {
              "movementBaseID": "base-5",
              "name": "Exercise 5"
            },
            "movementModifierID": "mod-5",
            "movementModifier": {
              "movementModifierID": "mod-5",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "9b222a78-d401-4162-a018-282c42ddc156",
                "movementID": "a178bf46-d56a-467d-9e8e-8ad9d2a978d2",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "578ad6cc-6fa9-4d40-8360-4d7d20e1486d",
                "movementID": "a178bf46-d56a-467d-9e8e-8ad9d2a978d2",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "ad149470-7fa5-43dc-8ff7-f8208f797431",
                "movementID": "a178bf46-d56a-467d-9e8e-8ad9d2a978d2",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          }
        ]
      },
      {
        "sessionID": "05067238-bab8-49e2-8ed0-b2f22727a9de",
        "programID": "a4311237-3f49-42d7-a4b5-51bbb31fb62e",
        "date": "2025-06-19",
        "movements": [
          {
            "movementID": "138e9205-f36f-4d93-b17b-b44cb9e81ffe",
            "trainingSessionID": "05067238-bab8-49e2-8ed0-b2f22727a9de",
            "movementBaseID": "base-1",
            "movementBase": {
              "movementBaseID": "base-1",
              "name": "Exercise 1"
            },
            "movementModifierID": "mod-1",
            "movementModifier": {
              "movementModifierID": "mod-1",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "c1d6f0fc-5f7b-4709-9fa3-afd9dd107983",
                "movementID": "138e9205-f36f-4d93-b17b-b44cb9e81ffe",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "a9b4fde3-c19f-45d6-843e-fb7fdbac6bef",
                "movementID": "138e9205-f36f-4d93-b17b-b44cb9e81ffe",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "d8a6c87f-d2cf-4969-9063-38695fd187d3",
                "movementID": "138e9205-f36f-4d93-b17b-b44cb9e81ffe",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "120d0fed-7adf-4840-860c-f285dab01530",
            "trainingSessionID": "05067238-bab8-49e2-8ed0-b2f22727a9de",
            "movementBaseID": "base-2",
            "movementBase": {
              "movementBaseID": "base-2",
              "name": "Exercise 2"
            },
            "movementModifierID": "mod-2",
            "movementModifier": {
              "movementModifierID": "mod-2",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "909f4228-4891-4ed2-afb6-edcfbf9d6d5f",
                "movementID": "120d0fed-7adf-4840-860c-f285dab01530",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "913e482a-1794-47e9-a022-d45f276779fb",
                "movementID": "120d0fed-7adf-4840-860c-f285dab01530",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "b76c40fc-c109-4d6c-b9d9-53ed73467b49",
                "movementID": "120d0fed-7adf-4840-860c-f285dab01530",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "2e20c371-d34f-436c-ad24-0d1f70e2e09c",
            "trainingSessionID": "05067238-bab8-49e2-8ed0-b2f22727a9de",
            "movementBaseID": "base-3",
            "movementBase": {
              "movementBaseID": "base-3",
              "name": "Exercise 3"
            },
            "movementModifierID": "mod-3",
            "movementModifier": {
              "movementModifierID": "mod-3",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "27b7c1c4-3f4c-4d41-b6ac-7330cde3f890",
                "movementID": "2e20c371-d34f-436c-ad24-0d1f70e2e09c",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "ca3b194b-0ed8-47fc-a853-97c4edc24215",
                "movementID": "2e20c371-d34f-436c-ad24-0d1f70e2e09c",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "66b96fe6-13dd-4660-b935-43d62eaf3541",
                "movementID": "2e20c371-d34f-436c-ad24-0d1f70e2e09c",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "c0585fc2-e251-4b1d-8457-b2b7c90f9346",
            "trainingSessionID": "05067238-bab8-49e2-8ed0-b2f22727a9de",
            "movementBaseID": "base-4",
            "movementBase": {
              "movementBaseID": "base-4",
              "name": "Exercise 4"
            },
            "movementModifierID": "mod-4",
            "movementModifier": {
              "movementModifierID": "mod-4",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "0430b2ed-1937-4fb2-9fb7-5e2f682f418b",
                "movementID": "c0585fc2-e251-4b1d-8457-b2b7c90f9346",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "e0231b91-fa3a-49b7-98c4-f96f43521724",
                "movementID": "c0585fc2-e251-4b1d-8457-b2b7c90f9346",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "dc8f722b-2ef8-4ef5-a7aa-3b530df5d7ea",
                "movementID": "c0585fc2-e251-4b1d-8457-b2b7c90f9346",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "4849aa72-de18-4ecc-89f4-4c56175cc821",
            "trainingSessionID": "05067238-bab8-49e2-8ed0-b2f22727a9de",
            "movementBaseID": "base-5",
            "movementBase": {
              "movementBaseID": "base-5",
              "name": "Exercise 5"
            },
            "movementModifierID": "mod-5",
            "movementModifier": {
              "movementModifierID": "mod-5",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "68f766f4-5e16-4a14-9d9e-c81434e56ad2",
                "movementID": "4849aa72-de18-4ecc-89f4-4c56175cc821",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "2a39f05c-536c-4716-a470-1c59c7615a03",
                "movementID": "4849aa72-de18-4ecc-89f4-4c56175cc821",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "2dbf599b-8d89-417b-a863-f35366ecc219",
                "movementID": "4849aa72-de18-4ecc-89f4-4c56175cc821",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          }
        ]
      }
    ]
  },
  {
    "programID": "a268bcef-d87c-43e6-aa5f-e8f3f6a2582c",
    "userID": "705fc0e8-334e-4772-843e-1b07504705a5",
    "title": "Lionheart Power Block 2",
    "startDate": "2025-06-01",
    "nextTrainingSessionDate": "2025-06-03",
    "endDate": "2025-06-21",
    "tags": [
      "Powerlifting"
    ],
    "trainingSessions": [
      {
        "sessionID": "f96d7bc5-0a25-4921-bb8f-c5433218f51a",
        "programID": "a268bcef-d87c-43e6-aa5f-e8f3f6a2582c",
        "date": "2025-06-01",
        "movements": [
          {
            "movementID": "f129b2bc-e022-48a5-93e5-de3b338f1b31",
            "trainingSessionID": "f96d7bc5-0a25-4921-bb8f-c5433218f51a",
            "movementBaseID": "base-1",
            "movementBase": {
              "movementBaseID": "base-1",
              "name": "Exercise 1"
            },
            "movementModifierID": "mod-1",
            "movementModifier": {
              "movementModifierID": "mod-1",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "d96c4302-3c0b-4317-aa2a-c17a031b4cb2",
                "movementID": "f129b2bc-e022-48a5-93e5-de3b338f1b31",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "458d6313-aec6-4dc0-b630-0ec961185b7f",
                "movementID": "f129b2bc-e022-48a5-93e5-de3b338f1b31",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "c976853f-51cd-4db8-98de-be151dc56034",
                "movementID": "f129b2bc-e022-48a5-93e5-de3b338f1b31",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "58e55d93-c7e4-4afd-a347-898c78890e52",
            "trainingSessionID": "f96d7bc5-0a25-4921-bb8f-c5433218f51a",
            "movementBaseID": "base-2",
            "movementBase": {
              "movementBaseID": "base-2",
              "name": "Exercise 2"
            },
            "movementModifierID": "mod-2",
            "movementModifier": {
              "movementModifierID": "mod-2",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "776a24c0-5d66-43f0-98d1-3f595e263e5b",
                "movementID": "58e55d93-c7e4-4afd-a347-898c78890e52",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "b469c8ad-909f-47c4-8050-10ffff8768cd",
                "movementID": "58e55d93-c7e4-4afd-a347-898c78890e52",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "ee7e2a79-f2cc-43fb-a6ca-d164594369e0",
                "movementID": "58e55d93-c7e4-4afd-a347-898c78890e52",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "5258887b-a9e5-48fe-9512-b0db516bf480",
            "trainingSessionID": "f96d7bc5-0a25-4921-bb8f-c5433218f51a",
            "movementBaseID": "base-3",
            "movementBase": {
              "movementBaseID": "base-3",
              "name": "Exercise 3"
            },
            "movementModifierID": "mod-3",
            "movementModifier": {
              "movementModifierID": "mod-3",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "9c26e8a8-6875-427b-b088-c5363d6a464a",
                "movementID": "5258887b-a9e5-48fe-9512-b0db516bf480",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "9546ab75-10e7-495f-b759-57716dfc93ce",
                "movementID": "5258887b-a9e5-48fe-9512-b0db516bf480",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "c6e3c3bc-d55d-43e7-9a62-054f985fb49f",
                "movementID": "5258887b-a9e5-48fe-9512-b0db516bf480",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "8df6d4ee-a2d9-4703-a208-8d80ad372df0",
            "trainingSessionID": "f96d7bc5-0a25-4921-bb8f-c5433218f51a",
            "movementBaseID": "base-4",
            "movementBase": {
              "movementBaseID": "base-4",
              "name": "Exercise 4"
            },
            "movementModifierID": "mod-4",
            "movementModifier": {
              "movementModifierID": "mod-4",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "47edaefc-ce1f-4fa1-a6bd-608072d532f7",
                "movementID": "8df6d4ee-a2d9-4703-a208-8d80ad372df0",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "1d6e1cc2-be79-4357-9b4e-b5bcbf60afdf",
                "movementID": "8df6d4ee-a2d9-4703-a208-8d80ad372df0",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "74bb94a9-d99b-442c-9574-5a5f4bf567dd",
                "movementID": "8df6d4ee-a2d9-4703-a208-8d80ad372df0",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "8e256ca5-2bb3-4218-8402-cefd7573af35",
            "trainingSessionID": "f96d7bc5-0a25-4921-bb8f-c5433218f51a",
            "movementBaseID": "base-5",
            "movementBase": {
              "movementBaseID": "base-5",
              "name": "Exercise 5"
            },
            "movementModifierID": "mod-5",
            "movementModifier": {
              "movementModifierID": "mod-5",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "ed98d031-28b2-40fc-b9ef-45a5e2ffcaf9",
                "movementID": "8e256ca5-2bb3-4218-8402-cefd7573af35",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "ccf89c6e-fc3b-406e-816f-12ecec9d80e9",
                "movementID": "8e256ca5-2bb3-4218-8402-cefd7573af35",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "6af04d5c-8d11-484e-bece-6251dc15f1de",
                "movementID": "8e256ca5-2bb3-4218-8402-cefd7573af35",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          }
        ]
      },
      {
        "sessionID": "d64b88d1-2509-47f5-a459-fb550c77989c",
        "programID": "a268bcef-d87c-43e6-aa5f-e8f3f6a2582c",
        "date": "2025-06-03",
        "movements": [
          {
            "movementID": "be4e3781-0311-48de-a12b-50e3c1a1b73b",
            "trainingSessionID": "d64b88d1-2509-47f5-a459-fb550c77989c",
            "movementBaseID": "base-1",
            "movementBase": {
              "movementBaseID": "base-1",
              "name": "Exercise 1"
            },
            "movementModifierID": "mod-1",
            "movementModifier": {
              "movementModifierID": "mod-1",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "21a73c53-f2db-4975-8b53-8f0d85351ca4",
                "movementID": "be4e3781-0311-48de-a12b-50e3c1a1b73b",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "944b0983-842f-45a6-b91c-576809b53a73",
                "movementID": "be4e3781-0311-48de-a12b-50e3c1a1b73b",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "877ef39c-200d-4d10-93a6-fdd1ee081865",
                "movementID": "be4e3781-0311-48de-a12b-50e3c1a1b73b",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "497dc9a0-a257-4164-b405-dabc51dbf733",
            "trainingSessionID": "d64b88d1-2509-47f5-a459-fb550c77989c",
            "movementBaseID": "base-2",
            "movementBase": {
              "movementBaseID": "base-2",
              "name": "Exercise 2"
            },
            "movementModifierID": "mod-2",
            "movementModifier": {
              "movementModifierID": "mod-2",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "7a4951ac-d32a-4f2d-9109-94912fba621c",
                "movementID": "497dc9a0-a257-4164-b405-dabc51dbf733",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "d2dd4781-d318-4444-b184-03f0b71f6b3d",
                "movementID": "497dc9a0-a257-4164-b405-dabc51dbf733",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "973ec2aa-24c8-4a35-a75b-67de650b1cc0",
                "movementID": "497dc9a0-a257-4164-b405-dabc51dbf733",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "0225870a-8b0d-4b45-99bf-b095b786fc42",
            "trainingSessionID": "d64b88d1-2509-47f5-a459-fb550c77989c",
            "movementBaseID": "base-3",
            "movementBase": {
              "movementBaseID": "base-3",
              "name": "Exercise 3"
            },
            "movementModifierID": "mod-3",
            "movementModifier": {
              "movementModifierID": "mod-3",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "7ab0cbc1-7209-4f2e-8cca-e4fced75b711",
                "movementID": "0225870a-8b0d-4b45-99bf-b095b786fc42",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "b316ce39-485f-4118-b4af-ed8f86dece42",
                "movementID": "0225870a-8b0d-4b45-99bf-b095b786fc42",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "2aafd3d5-f820-4712-8d22-8a12be9087b8",
                "movementID": "0225870a-8b0d-4b45-99bf-b095b786fc42",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "42f6061d-29cc-4201-b1a4-d88fc0efaf32",
            "trainingSessionID": "d64b88d1-2509-47f5-a459-fb550c77989c",
            "movementBaseID": "base-4",
            "movementBase": {
              "movementBaseID": "base-4",
              "name": "Exercise 4"
            },
            "movementModifierID": "mod-4",
            "movementModifier": {
              "movementModifierID": "mod-4",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "21d01418-229d-47b6-93b0-093eff7a4e36",
                "movementID": "42f6061d-29cc-4201-b1a4-d88fc0efaf32",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "7ff56821-8021-433f-b3d4-af0b01fe2524",
                "movementID": "42f6061d-29cc-4201-b1a4-d88fc0efaf32",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "27c79fbf-a3a2-480c-9843-7ed700be364c",
                "movementID": "42f6061d-29cc-4201-b1a4-d88fc0efaf32",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "6266d90b-ab0d-4544-9e7b-40c7df462220",
            "trainingSessionID": "d64b88d1-2509-47f5-a459-fb550c77989c",
            "movementBaseID": "base-5",
            "movementBase": {
              "movementBaseID": "base-5",
              "name": "Exercise 5"
            },
            "movementModifierID": "mod-5",
            "movementModifier": {
              "movementModifierID": "mod-5",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "e7e925c9-17d9-447d-bcd1-67181a014b95",
                "movementID": "6266d90b-ab0d-4544-9e7b-40c7df462220",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "d8ea2fda-87bc-41ed-860c-55189ce2f6ad",
                "movementID": "6266d90b-ab0d-4544-9e7b-40c7df462220",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "772a8df9-9a74-4d3f-98c4-a66a2c4b82f5",
                "movementID": "6266d90b-ab0d-4544-9e7b-40c7df462220",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          }
        ]
      },
      {
        "sessionID": "a29a0ddb-3792-4c33-8bb7-4563f42dff96",
        "programID": "a268bcef-d87c-43e6-aa5f-e8f3f6a2582c",
        "date": "2025-06-05",
        "movements": [
          {
            "movementID": "a6a5952d-c20c-479f-9942-694180558000",
            "trainingSessionID": "a29a0ddb-3792-4c33-8bb7-4563f42dff96",
            "movementBaseID": "base-1",
            "movementBase": {
              "movementBaseID": "base-1",
              "name": "Exercise 1"
            },
            "movementModifierID": "mod-1",
            "movementModifier": {
              "movementModifierID": "mod-1",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "9e7ce152-a6c8-40c7-be22-14ffdf6b6f41",
                "movementID": "a6a5952d-c20c-479f-9942-694180558000",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "2501debc-defb-4966-870f-2231f43fd98c",
                "movementID": "a6a5952d-c20c-479f-9942-694180558000",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "d52643ba-5f7d-487a-a6c5-e18166c8942f",
                "movementID": "a6a5952d-c20c-479f-9942-694180558000",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "d282baa4-a640-454e-90ad-250b16ab778f",
            "trainingSessionID": "a29a0ddb-3792-4c33-8bb7-4563f42dff96",
            "movementBaseID": "base-2",
            "movementBase": {
              "movementBaseID": "base-2",
              "name": "Exercise 2"
            },
            "movementModifierID": "mod-2",
            "movementModifier": {
              "movementModifierID": "mod-2",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "62263039-f10a-4291-a589-9ba44dc56f8a",
                "movementID": "d282baa4-a640-454e-90ad-250b16ab778f",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "67bfff3e-9625-4634-9c7e-a9498bbe104b",
                "movementID": "d282baa4-a640-454e-90ad-250b16ab778f",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "003215e5-02fd-4a2b-ac88-9278801de9b6",
                "movementID": "d282baa4-a640-454e-90ad-250b16ab778f",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "8a5b2185-e0e4-4084-996e-ff7e81380400",
            "trainingSessionID": "a29a0ddb-3792-4c33-8bb7-4563f42dff96",
            "movementBaseID": "base-3",
            "movementBase": {
              "movementBaseID": "base-3",
              "name": "Exercise 3"
            },
            "movementModifierID": "mod-3",
            "movementModifier": {
              "movementModifierID": "mod-3",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "6d4d1b07-bb9e-4f86-9ba9-f012bae33f0f",
                "movementID": "8a5b2185-e0e4-4084-996e-ff7e81380400",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "f9e9dfb6-b118-43c0-9c8c-4bc668c493ba",
                "movementID": "8a5b2185-e0e4-4084-996e-ff7e81380400",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "840a68e2-0c65-4b93-9443-be0452bf1344",
                "movementID": "8a5b2185-e0e4-4084-996e-ff7e81380400",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "daf4ebe9-d7a3-41bc-bef3-120cc5f4d63d",
            "trainingSessionID": "a29a0ddb-3792-4c33-8bb7-4563f42dff96",
            "movementBaseID": "base-4",
            "movementBase": {
              "movementBaseID": "base-4",
              "name": "Exercise 4"
            },
            "movementModifierID": "mod-4",
            "movementModifier": {
              "movementModifierID": "mod-4",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "5eb68057-c0e1-4e8a-a506-c648574ae283",
                "movementID": "daf4ebe9-d7a3-41bc-bef3-120cc5f4d63d",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "d709b63a-42a8-47ad-b51d-bf0463ab24c6",
                "movementID": "daf4ebe9-d7a3-41bc-bef3-120cc5f4d63d",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "9cb4b38c-48a3-4b17-a11c-90748774f0ee",
                "movementID": "daf4ebe9-d7a3-41bc-bef3-120cc5f4d63d",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "ae608834-4fff-4f33-8177-a9eec6b01a7b",
            "trainingSessionID": "a29a0ddb-3792-4c33-8bb7-4563f42dff96",
            "movementBaseID": "base-5",
            "movementBase": {
              "movementBaseID": "base-5",
              "name": "Exercise 5"
            },
            "movementModifierID": "mod-5",
            "movementModifier": {
              "movementModifierID": "mod-5",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "b549b4f0-990b-48ab-81f1-5aad279eea4a",
                "movementID": "ae608834-4fff-4f33-8177-a9eec6b01a7b",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "9d6918ad-4e46-422a-922b-b4ebe5c22624",
                "movementID": "ae608834-4fff-4f33-8177-a9eec6b01a7b",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "efdcf95d-825d-48b7-86e8-cf5ca7426bdd",
                "movementID": "ae608834-4fff-4f33-8177-a9eec6b01a7b",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          }
        ]
      },
      {
        "sessionID": "8643a3f7-9017-4042-a64b-888ddec5e19c",
        "programID": "a268bcef-d87c-43e6-aa5f-e8f3f6a2582c",
        "date": "2025-06-07",
        "movements": [
          {
            "movementID": "0d70de09-2bba-488c-b8ab-34f836c9161f",
            "trainingSessionID": "8643a3f7-9017-4042-a64b-888ddec5e19c",
            "movementBaseID": "base-1",
            "movementBase": {
              "movementBaseID": "base-1",
              "name": "Exercise 1"
            },
            "movementModifierID": "mod-1",
            "movementModifier": {
              "movementModifierID": "mod-1",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "d805306e-d185-428f-9689-c20e30bac7c9",
                "movementID": "0d70de09-2bba-488c-b8ab-34f836c9161f",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "926f1701-d361-4c57-a06c-e50e6b939123",
                "movementID": "0d70de09-2bba-488c-b8ab-34f836c9161f",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "6aafd890-456f-47fe-98f3-4bbd51c7ca68",
                "movementID": "0d70de09-2bba-488c-b8ab-34f836c9161f",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "5f7e0150-7939-4b7d-bbe4-a7e220af7238",
            "trainingSessionID": "8643a3f7-9017-4042-a64b-888ddec5e19c",
            "movementBaseID": "base-2",
            "movementBase": {
              "movementBaseID": "base-2",
              "name": "Exercise 2"
            },
            "movementModifierID": "mod-2",
            "movementModifier": {
              "movementModifierID": "mod-2",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "f57a834b-c8c4-444d-b53a-0cd96dcd1e10",
                "movementID": "5f7e0150-7939-4b7d-bbe4-a7e220af7238",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "0f9d4d43-3900-4472-a5c4-af8a8adbcc82",
                "movementID": "5f7e0150-7939-4b7d-bbe4-a7e220af7238",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "de77827e-de83-44c4-a9e8-4d9b02e4c83d",
                "movementID": "5f7e0150-7939-4b7d-bbe4-a7e220af7238",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "f81692ca-b6bc-4985-b10d-5add3b8a2bf9",
            "trainingSessionID": "8643a3f7-9017-4042-a64b-888ddec5e19c",
            "movementBaseID": "base-3",
            "movementBase": {
              "movementBaseID": "base-3",
              "name": "Exercise 3"
            },
            "movementModifierID": "mod-3",
            "movementModifier": {
              "movementModifierID": "mod-3",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "51e93c52-f6a6-4cd7-9de9-ce6be9291f65",
                "movementID": "f81692ca-b6bc-4985-b10d-5add3b8a2bf9",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "5a715d55-a101-4f52-9910-eaa677ef0985",
                "movementID": "f81692ca-b6bc-4985-b10d-5add3b8a2bf9",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "9febf837-f1fd-4495-9f7d-7a7c8f0b835a",
                "movementID": "f81692ca-b6bc-4985-b10d-5add3b8a2bf9",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "c3a57d0f-2c28-48a1-8f29-960509854cec",
            "trainingSessionID": "8643a3f7-9017-4042-a64b-888ddec5e19c",
            "movementBaseID": "base-4",
            "movementBase": {
              "movementBaseID": "base-4",
              "name": "Exercise 4"
            },
            "movementModifierID": "mod-4",
            "movementModifier": {
              "movementModifierID": "mod-4",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "412f406c-0aac-4127-9d17-483c23aa60ef",
                "movementID": "c3a57d0f-2c28-48a1-8f29-960509854cec",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "74349988-6155-414c-8ef1-2840b6b32e21",
                "movementID": "c3a57d0f-2c28-48a1-8f29-960509854cec",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "0f6a5a3a-4de7-4926-b312-d8d6890235be",
                "movementID": "c3a57d0f-2c28-48a1-8f29-960509854cec",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "e1c65a4b-4e48-4a0a-ba07-e00e1eba5523",
            "trainingSessionID": "8643a3f7-9017-4042-a64b-888ddec5e19c",
            "movementBaseID": "base-5",
            "movementBase": {
              "movementBaseID": "base-5",
              "name": "Exercise 5"
            },
            "movementModifierID": "mod-5",
            "movementModifier": {
              "movementModifierID": "mod-5",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "98a47d60-8ab2-42cd-b764-adb26528f10b",
                "movementID": "e1c65a4b-4e48-4a0a-ba07-e00e1eba5523",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "894a7365-08fb-40d0-83e9-5db15018f790",
                "movementID": "e1c65a4b-4e48-4a0a-ba07-e00e1eba5523",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "3a682e1b-6b10-4263-a201-9d20082b6506",
                "movementID": "e1c65a4b-4e48-4a0a-ba07-e00e1eba5523",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          }
        ]
      },
      {
        "sessionID": "58f0a191-1366-4333-ad49-0d5acdc30c28",
        "programID": "a268bcef-d87c-43e6-aa5f-e8f3f6a2582c",
        "date": "2025-06-09",
        "movements": [
          {
            "movementID": "4530f724-5a21-4d43-97ba-1d7540a4932a",
            "trainingSessionID": "58f0a191-1366-4333-ad49-0d5acdc30c28",
            "movementBaseID": "base-1",
            "movementBase": {
              "movementBaseID": "base-1",
              "name": "Exercise 1"
            },
            "movementModifierID": "mod-1",
            "movementModifier": {
              "movementModifierID": "mod-1",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "80cba0ce-d828-4b6d-936b-30c4e09a5e60",
                "movementID": "4530f724-5a21-4d43-97ba-1d7540a4932a",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "3838649f-8435-4f0e-984c-3f1d6a4b2a44",
                "movementID": "4530f724-5a21-4d43-97ba-1d7540a4932a",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "b84b830a-8e9f-4c9b-864a-8f18bd9712e0",
                "movementID": "4530f724-5a21-4d43-97ba-1d7540a4932a",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "4ec9a5e1-dcb5-4007-9f59-9f7e3d4ce231",
            "trainingSessionID": "58f0a191-1366-4333-ad49-0d5acdc30c28",
            "movementBaseID": "base-2",
            "movementBase": {
              "movementBaseID": "base-2",
              "name": "Exercise 2"
            },
            "movementModifierID": "mod-2",
            "movementModifier": {
              "movementModifierID": "mod-2",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "e7749cd6-b920-4927-be11-8376b8e3a625",
                "movementID": "4ec9a5e1-dcb5-4007-9f59-9f7e3d4ce231",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "9d7c4dc9-5d7f-475d-b0f0-1d32fd7b9c3d",
                "movementID": "4ec9a5e1-dcb5-4007-9f59-9f7e3d4ce231",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "a026bc17-69b5-478c-b100-fcfe372c9f83",
                "movementID": "4ec9a5e1-dcb5-4007-9f59-9f7e3d4ce231",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "989d4e94-6fc4-4359-bb08-82d1523a3da9",
            "trainingSessionID": "58f0a191-1366-4333-ad49-0d5acdc30c28",
            "movementBaseID": "base-3",
            "movementBase": {
              "movementBaseID": "base-3",
              "name": "Exercise 3"
            },
            "movementModifierID": "mod-3",
            "movementModifier": {
              "movementModifierID": "mod-3",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "37444ba7-1904-47b0-b8a0-30f96a01a382",
                "movementID": "989d4e94-6fc4-4359-bb08-82d1523a3da9",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "cd4ef886-14af-4d50-894f-80c072029d2e",
                "movementID": "989d4e94-6fc4-4359-bb08-82d1523a3da9",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "024746c2-4eb9-4252-95ce-3e7519a984aa",
                "movementID": "989d4e94-6fc4-4359-bb08-82d1523a3da9",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "e798584d-48ff-4460-95a2-3cbdaf55dee6",
            "trainingSessionID": "58f0a191-1366-4333-ad49-0d5acdc30c28",
            "movementBaseID": "base-4",
            "movementBase": {
              "movementBaseID": "base-4",
              "name": "Exercise 4"
            },
            "movementModifierID": "mod-4",
            "movementModifier": {
              "movementModifierID": "mod-4",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "6d98a5d9-12ee-4a5d-88a4-b4bfcbf5164a",
                "movementID": "e798584d-48ff-4460-95a2-3cbdaf55dee6",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "3c4049a0-5909-4fc6-806c-4dfda9de06d3",
                "movementID": "e798584d-48ff-4460-95a2-3cbdaf55dee6",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "9001d175-d58b-4005-b48f-91c5122bc814",
                "movementID": "e798584d-48ff-4460-95a2-3cbdaf55dee6",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "973243bb-4c61-4366-9396-e91d5e579418",
            "trainingSessionID": "58f0a191-1366-4333-ad49-0d5acdc30c28",
            "movementBaseID": "base-5",
            "movementBase": {
              "movementBaseID": "base-5",
              "name": "Exercise 5"
            },
            "movementModifierID": "mod-5",
            "movementModifier": {
              "movementModifierID": "mod-5",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "bd382f5a-ea9f-41d2-8ca0-99b83ca44961",
                "movementID": "973243bb-4c61-4366-9396-e91d5e579418",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "020724a9-7a2e-4f6e-9e0c-7638705912d0",
                "movementID": "973243bb-4c61-4366-9396-e91d5e579418",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "0692a7bf-0f37-431f-bf5e-8118d7f67520",
                "movementID": "973243bb-4c61-4366-9396-e91d5e579418",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          }
        ]
      },
      {
        "sessionID": "99511031-b787-40c6-a2bb-14e00b7c263c",
        "programID": "a268bcef-d87c-43e6-aa5f-e8f3f6a2582c",
        "date": "2025-06-11",
        "movements": [
          {
            "movementID": "6cc62a32-104d-47c9-8dcd-724ba97597ef",
            "trainingSessionID": "99511031-b787-40c6-a2bb-14e00b7c263c",
            "movementBaseID": "base-1",
            "movementBase": {
              "movementBaseID": "base-1",
              "name": "Exercise 1"
            },
            "movementModifierID": "mod-1",
            "movementModifier": {
              "movementModifierID": "mod-1",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "0f754cdb-f365-4bc7-8015-27eb1938e406",
                "movementID": "6cc62a32-104d-47c9-8dcd-724ba97597ef",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "409b8bf5-ab4a-4828-9332-c9f1f7a79a62",
                "movementID": "6cc62a32-104d-47c9-8dcd-724ba97597ef",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "7dc72b3b-4f20-4de0-b533-b343a1d3ec30",
                "movementID": "6cc62a32-104d-47c9-8dcd-724ba97597ef",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "59517217-3755-4310-be97-019aca24c90d",
            "trainingSessionID": "99511031-b787-40c6-a2bb-14e00b7c263c",
            "movementBaseID": "base-2",
            "movementBase": {
              "movementBaseID": "base-2",
              "name": "Exercise 2"
            },
            "movementModifierID": "mod-2",
            "movementModifier": {
              "movementModifierID": "mod-2",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "549fd625-4ea0-43ab-a62a-3160736cf79e",
                "movementID": "59517217-3755-4310-be97-019aca24c90d",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "06ec1f50-fe7c-4c96-8c6e-137167213a50",
                "movementID": "59517217-3755-4310-be97-019aca24c90d",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "18cad9a4-e6cc-437c-8c5b-915ea36b93e9",
                "movementID": "59517217-3755-4310-be97-019aca24c90d",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "ddcb14d4-660f-4346-8dc4-f875050ab6fa",
            "trainingSessionID": "99511031-b787-40c6-a2bb-14e00b7c263c",
            "movementBaseID": "base-3",
            "movementBase": {
              "movementBaseID": "base-3",
              "name": "Exercise 3"
            },
            "movementModifierID": "mod-3",
            "movementModifier": {
              "movementModifierID": "mod-3",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "06a709e7-62a1-4d9f-b71c-ba9787abe32f",
                "movementID": "ddcb14d4-660f-4346-8dc4-f875050ab6fa",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "d512c50e-fca6-4ace-9a8e-32b82eb03626",
                "movementID": "ddcb14d4-660f-4346-8dc4-f875050ab6fa",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "4d462829-8714-4ed0-a5a3-a73b30f03ba1",
                "movementID": "ddcb14d4-660f-4346-8dc4-f875050ab6fa",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "248facea-94e5-4ddd-b64a-43a04756b786",
            "trainingSessionID": "99511031-b787-40c6-a2bb-14e00b7c263c",
            "movementBaseID": "base-4",
            "movementBase": {
              "movementBaseID": "base-4",
              "name": "Exercise 4"
            },
            "movementModifierID": "mod-4",
            "movementModifier": {
              "movementModifierID": "mod-4",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "2a98abfa-6db2-457a-a41f-e75bc825b13b",
                "movementID": "248facea-94e5-4ddd-b64a-43a04756b786",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "6c073cc6-a60b-4d62-8e8b-5963a593328d",
                "movementID": "248facea-94e5-4ddd-b64a-43a04756b786",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "a9c81442-9cfa-43b2-ad4e-46a7a92cac49",
                "movementID": "248facea-94e5-4ddd-b64a-43a04756b786",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "352d0afe-6bb1-4eb5-9190-d902451a6b3b",
            "trainingSessionID": "99511031-b787-40c6-a2bb-14e00b7c263c",
            "movementBaseID": "base-5",
            "movementBase": {
              "movementBaseID": "base-5",
              "name": "Exercise 5"
            },
            "movementModifierID": "mod-5",
            "movementModifier": {
              "movementModifierID": "mod-5",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "ff781e60-6eb0-4a86-8b2c-eb4944e8a174",
                "movementID": "352d0afe-6bb1-4eb5-9190-d902451a6b3b",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "e248bb10-9bed-4f99-8f1a-d85a3eee5889",
                "movementID": "352d0afe-6bb1-4eb5-9190-d902451a6b3b",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "1e99e691-b6b1-4a10-a6c4-dda7469cc6d2",
                "movementID": "352d0afe-6bb1-4eb5-9190-d902451a6b3b",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          }
        ]
      },
      {
        "sessionID": "5bb16bd6-a4ac-4b64-bfcf-2472f0c7fcb3",
        "programID": "a268bcef-d87c-43e6-aa5f-e8f3f6a2582c",
        "date": "2025-06-13",
        "movements": [
          {
            "movementID": "73daacd1-8e82-4fb7-b928-fa1d54921252",
            "trainingSessionID": "5bb16bd6-a4ac-4b64-bfcf-2472f0c7fcb3",
            "movementBaseID": "base-1",
            "movementBase": {
              "movementBaseID": "base-1",
              "name": "Exercise 1"
            },
            "movementModifierID": "mod-1",
            "movementModifier": {
              "movementModifierID": "mod-1",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "aea7fbf1-a86c-4465-91ab-eace344d5b47",
                "movementID": "73daacd1-8e82-4fb7-b928-fa1d54921252",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "755e5fff-c164-4aa0-9388-ac69990b0367",
                "movementID": "73daacd1-8e82-4fb7-b928-fa1d54921252",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "84e4fe69-a6d4-4714-83e8-0951779c5342",
                "movementID": "73daacd1-8e82-4fb7-b928-fa1d54921252",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "dc0f87e4-b6cd-4c1f-9206-3bf41fdb4cbf",
            "trainingSessionID": "5bb16bd6-a4ac-4b64-bfcf-2472f0c7fcb3",
            "movementBaseID": "base-2",
            "movementBase": {
              "movementBaseID": "base-2",
              "name": "Exercise 2"
            },
            "movementModifierID": "mod-2",
            "movementModifier": {
              "movementModifierID": "mod-2",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "b32828ad-f8f4-4c1f-9d9c-616422aaafb6",
                "movementID": "dc0f87e4-b6cd-4c1f-9206-3bf41fdb4cbf",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "20933761-33dd-44dd-98bf-912e91b87aaf",
                "movementID": "dc0f87e4-b6cd-4c1f-9206-3bf41fdb4cbf",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "dfc66779-9345-4c6e-b125-2ea3645845df",
                "movementID": "dc0f87e4-b6cd-4c1f-9206-3bf41fdb4cbf",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "0d3d62ef-182b-46fd-ad42-ebda176106f9",
            "trainingSessionID": "5bb16bd6-a4ac-4b64-bfcf-2472f0c7fcb3",
            "movementBaseID": "base-3",
            "movementBase": {
              "movementBaseID": "base-3",
              "name": "Exercise 3"
            },
            "movementModifierID": "mod-3",
            "movementModifier": {
              "movementModifierID": "mod-3",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "d986711a-6ed9-434d-a8fa-d035f99ac8cb",
                "movementID": "0d3d62ef-182b-46fd-ad42-ebda176106f9",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "4e15afb1-dcb0-4384-932e-a0cf6f4ae46d",
                "movementID": "0d3d62ef-182b-46fd-ad42-ebda176106f9",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "435dbe4b-864e-42a8-a820-237a1ad216d7",
                "movementID": "0d3d62ef-182b-46fd-ad42-ebda176106f9",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "e5f30116-4a12-4ad8-bcbd-6010c18d6874",
            "trainingSessionID": "5bb16bd6-a4ac-4b64-bfcf-2472f0c7fcb3",
            "movementBaseID": "base-4",
            "movementBase": {
              "movementBaseID": "base-4",
              "name": "Exercise 4"
            },
            "movementModifierID": "mod-4",
            "movementModifier": {
              "movementModifierID": "mod-4",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "179509ed-e06b-4286-b495-13703282c49f",
                "movementID": "e5f30116-4a12-4ad8-bcbd-6010c18d6874",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "3ce62b3f-f56a-4e9e-9cdb-7686b1b121f9",
                "movementID": "e5f30116-4a12-4ad8-bcbd-6010c18d6874",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "70dc4a8d-c9b9-4af1-abdc-8d475bbc3777",
                "movementID": "e5f30116-4a12-4ad8-bcbd-6010c18d6874",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "5e47bdee-d8d8-407f-9c3d-9b1d951f189b",
            "trainingSessionID": "5bb16bd6-a4ac-4b64-bfcf-2472f0c7fcb3",
            "movementBaseID": "base-5",
            "movementBase": {
              "movementBaseID": "base-5",
              "name": "Exercise 5"
            },
            "movementModifierID": "mod-5",
            "movementModifier": {
              "movementModifierID": "mod-5",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "f07b22a3-1bb8-4fa4-9a7e-1ea76c9e0ea4",
                "movementID": "5e47bdee-d8d8-407f-9c3d-9b1d951f189b",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "04a9139b-ef21-4165-a4a3-3834cc892776",
                "movementID": "5e47bdee-d8d8-407f-9c3d-9b1d951f189b",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "48f70aa6-d07a-4458-a264-9149bd9d6c00",
                "movementID": "5e47bdee-d8d8-407f-9c3d-9b1d951f189b",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          }
        ]
      },
      {
        "sessionID": "e12aea4c-67d5-45ab-8046-daa72e3915c6",
        "programID": "a268bcef-d87c-43e6-aa5f-e8f3f6a2582c",
        "date": "2025-06-15",
        "movements": [
          {
            "movementID": "4c5d926e-8704-40f0-88bc-eb3215cd7283",
            "trainingSessionID": "e12aea4c-67d5-45ab-8046-daa72e3915c6",
            "movementBaseID": "base-1",
            "movementBase": {
              "movementBaseID": "base-1",
              "name": "Exercise 1"
            },
            "movementModifierID": "mod-1",
            "movementModifier": {
              "movementModifierID": "mod-1",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "8ce0f08d-1e89-4442-9424-cca595828d91",
                "movementID": "4c5d926e-8704-40f0-88bc-eb3215cd7283",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "9f6d2bd6-435a-41fb-8050-a7bb951c1268",
                "movementID": "4c5d926e-8704-40f0-88bc-eb3215cd7283",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "c837a2fd-3276-40a2-82f8-d0cbc7459902",
                "movementID": "4c5d926e-8704-40f0-88bc-eb3215cd7283",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "5967a1a3-ca27-4c27-b7a9-825afa6be848",
            "trainingSessionID": "e12aea4c-67d5-45ab-8046-daa72e3915c6",
            "movementBaseID": "base-2",
            "movementBase": {
              "movementBaseID": "base-2",
              "name": "Exercise 2"
            },
            "movementModifierID": "mod-2",
            "movementModifier": {
              "movementModifierID": "mod-2",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "3a7b50e1-dbf1-4972-9426-2cb1f2d890e1",
                "movementID": "5967a1a3-ca27-4c27-b7a9-825afa6be848",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "540be1a9-ae22-4631-af18-6104b11dcefc",
                "movementID": "5967a1a3-ca27-4c27-b7a9-825afa6be848",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "bf539898-8f29-4aa2-b429-95170f2c52b7",
                "movementID": "5967a1a3-ca27-4c27-b7a9-825afa6be848",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "102db17e-f652-4de8-860d-14b0904d4218",
            "trainingSessionID": "e12aea4c-67d5-45ab-8046-daa72e3915c6",
            "movementBaseID": "base-3",
            "movementBase": {
              "movementBaseID": "base-3",
              "name": "Exercise 3"
            },
            "movementModifierID": "mod-3",
            "movementModifier": {
              "movementModifierID": "mod-3",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "2bfd5db6-dcd0-47fb-8bbc-5c2dcd43c869",
                "movementID": "102db17e-f652-4de8-860d-14b0904d4218",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "daba8620-ca2c-4f32-93b5-8de854a2cacb",
                "movementID": "102db17e-f652-4de8-860d-14b0904d4218",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "d566bfdf-cd9e-4213-8ed5-f37167b72ec4",
                "movementID": "102db17e-f652-4de8-860d-14b0904d4218",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "7a3693ef-b9e9-4702-b2c6-1506f089f1b7",
            "trainingSessionID": "e12aea4c-67d5-45ab-8046-daa72e3915c6",
            "movementBaseID": "base-4",
            "movementBase": {
              "movementBaseID": "base-4",
              "name": "Exercise 4"
            },
            "movementModifierID": "mod-4",
            "movementModifier": {
              "movementModifierID": "mod-4",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "3dd47ada-c7c5-456b-9a03-7b57ea8e0f1b",
                "movementID": "7a3693ef-b9e9-4702-b2c6-1506f089f1b7",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "089b0526-a6a7-4c91-ac44-1257990bd6e2",
                "movementID": "7a3693ef-b9e9-4702-b2c6-1506f089f1b7",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "af17fc8e-9977-4ab1-836e-d1e0e680fc9d",
                "movementID": "7a3693ef-b9e9-4702-b2c6-1506f089f1b7",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "9f301138-fd00-4046-8c46-059331f02c50",
            "trainingSessionID": "e12aea4c-67d5-45ab-8046-daa72e3915c6",
            "movementBaseID": "base-5",
            "movementBase": {
              "movementBaseID": "base-5",
              "name": "Exercise 5"
            },
            "movementModifierID": "mod-5",
            "movementModifier": {
              "movementModifierID": "mod-5",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "1188bfdc-0007-4c93-8561-d345e2913a83",
                "movementID": "9f301138-fd00-4046-8c46-059331f02c50",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "70b57bcf-2e2f-45b7-beff-98ca01d34e4a",
                "movementID": "9f301138-fd00-4046-8c46-059331f02c50",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "14e4ad55-002b-43bf-9eb6-6fc342378a75",
                "movementID": "9f301138-fd00-4046-8c46-059331f02c50",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          }
        ]
      },
      {
        "sessionID": "5e0e8004-ae05-4472-b8e0-835b0b36a3f4",
        "programID": "a268bcef-d87c-43e6-aa5f-e8f3f6a2582c",
        "date": "2025-06-17",
        "movements": [
          {
            "movementID": "ad62ee21-cacd-4387-b979-b309279dd8de",
            "trainingSessionID": "5e0e8004-ae05-4472-b8e0-835b0b36a3f4",
            "movementBaseID": "base-1",
            "movementBase": {
              "movementBaseID": "base-1",
              "name": "Exercise 1"
            },
            "movementModifierID": "mod-1",
            "movementModifier": {
              "movementModifierID": "mod-1",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "74e9168c-681a-4763-a8a4-ed9d3780152f",
                "movementID": "ad62ee21-cacd-4387-b979-b309279dd8de",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "eeacc193-ec01-4971-9259-a93641b7e0bc",
                "movementID": "ad62ee21-cacd-4387-b979-b309279dd8de",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "eb0fc258-6054-4d18-8936-edba20d7d800",
                "movementID": "ad62ee21-cacd-4387-b979-b309279dd8de",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "248b2512-5163-4505-8540-44a44a303b3a",
            "trainingSessionID": "5e0e8004-ae05-4472-b8e0-835b0b36a3f4",
            "movementBaseID": "base-2",
            "movementBase": {
              "movementBaseID": "base-2",
              "name": "Exercise 2"
            },
            "movementModifierID": "mod-2",
            "movementModifier": {
              "movementModifierID": "mod-2",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "e3cbbce3-a835-4d6b-b0e8-851fa078b15b",
                "movementID": "248b2512-5163-4505-8540-44a44a303b3a",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "e31309a1-b0e8-4045-8ea8-b4ddedd0a753",
                "movementID": "248b2512-5163-4505-8540-44a44a303b3a",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "ecfd1318-1183-4ad1-aa82-400c437e4c2c",
                "movementID": "248b2512-5163-4505-8540-44a44a303b3a",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "3419543e-dd9f-4593-b550-2879810fa3a8",
            "trainingSessionID": "5e0e8004-ae05-4472-b8e0-835b0b36a3f4",
            "movementBaseID": "base-3",
            "movementBase": {
              "movementBaseID": "base-3",
              "name": "Exercise 3"
            },
            "movementModifierID": "mod-3",
            "movementModifier": {
              "movementModifierID": "mod-3",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "54c64ec0-81fd-4391-9892-77b47c892cc8",
                "movementID": "3419543e-dd9f-4593-b550-2879810fa3a8",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "07cc906c-a690-4464-852e-9a5a95a35e5d",
                "movementID": "3419543e-dd9f-4593-b550-2879810fa3a8",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "f96d790e-68e1-41de-82cb-5bd441e9937a",
                "movementID": "3419543e-dd9f-4593-b550-2879810fa3a8",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "bfbed3b7-d5f3-4cd4-bc16-5c2c9ee3f0c0",
            "trainingSessionID": "5e0e8004-ae05-4472-b8e0-835b0b36a3f4",
            "movementBaseID": "base-4",
            "movementBase": {
              "movementBaseID": "base-4",
              "name": "Exercise 4"
            },
            "movementModifierID": "mod-4",
            "movementModifier": {
              "movementModifierID": "mod-4",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "6a08fe8b-c276-447d-b382-e411cdb6db3c",
                "movementID": "bfbed3b7-d5f3-4cd4-bc16-5c2c9ee3f0c0",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "ce2dc7b5-ec80-4b98-bf21-0f973d59edfe",
                "movementID": "bfbed3b7-d5f3-4cd4-bc16-5c2c9ee3f0c0",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "6a10eb75-9a8f-4b77-a775-75d4d234b235",
                "movementID": "bfbed3b7-d5f3-4cd4-bc16-5c2c9ee3f0c0",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "5ba93f81-cc75-4c61-898e-7dcee3231f8b",
            "trainingSessionID": "5e0e8004-ae05-4472-b8e0-835b0b36a3f4",
            "movementBaseID": "base-5",
            "movementBase": {
              "movementBaseID": "base-5",
              "name": "Exercise 5"
            },
            "movementModifierID": "mod-5",
            "movementModifier": {
              "movementModifierID": "mod-5",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "6c0a7d65-532b-4402-833e-18ccea56383f",
                "movementID": "5ba93f81-cc75-4c61-898e-7dcee3231f8b",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "9536d120-834c-4cc1-bdf9-1b6d426d68b9",
                "movementID": "5ba93f81-cc75-4c61-898e-7dcee3231f8b",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "71653d71-83d3-4462-8498-9c2ed4e9cf78",
                "movementID": "5ba93f81-cc75-4c61-898e-7dcee3231f8b",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          }
        ]
      },
      {
        "sessionID": "8867785d-8984-49a2-96fe-674429c3db71",
        "programID": "a268bcef-d87c-43e6-aa5f-e8f3f6a2582c",
        "date": "2025-06-19",
        "movements": [
          {
            "movementID": "82017545-962d-4e34-a9d9-448c6f19c3ba",
            "trainingSessionID": "8867785d-8984-49a2-96fe-674429c3db71",
            "movementBaseID": "base-1",
            "movementBase": {
              "movementBaseID": "base-1",
              "name": "Exercise 1"
            },
            "movementModifierID": "mod-1",
            "movementModifier": {
              "movementModifierID": "mod-1",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "01305bfa-e5c7-4fc5-a9e8-54e2daafe36b",
                "movementID": "82017545-962d-4e34-a9d9-448c6f19c3ba",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "9a07112d-a61c-4cb6-9050-f0e0fc5de92b",
                "movementID": "82017545-962d-4e34-a9d9-448c6f19c3ba",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "56e2cce3-ed25-4494-893d-d97e94a66a3c",
                "movementID": "82017545-962d-4e34-a9d9-448c6f19c3ba",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "3c795a80-1fdc-4d09-8552-1492517aed69",
            "trainingSessionID": "8867785d-8984-49a2-96fe-674429c3db71",
            "movementBaseID": "base-2",
            "movementBase": {
              "movementBaseID": "base-2",
              "name": "Exercise 2"
            },
            "movementModifierID": "mod-2",
            "movementModifier": {
              "movementModifierID": "mod-2",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "18eab01a-aefa-4316-aa4f-b816e528a777",
                "movementID": "3c795a80-1fdc-4d09-8552-1492517aed69",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "e2991dcf-4302-427e-bebe-caca1b90970d",
                "movementID": "3c795a80-1fdc-4d09-8552-1492517aed69",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "820d4da5-de4d-41de-9a9b-373fc7809dfa",
                "movementID": "3c795a80-1fdc-4d09-8552-1492517aed69",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "dbad8f2c-d202-4ce6-8fe3-fc739366c1af",
            "trainingSessionID": "8867785d-8984-49a2-96fe-674429c3db71",
            "movementBaseID": "base-3",
            "movementBase": {
              "movementBaseID": "base-3",
              "name": "Exercise 3"
            },
            "movementModifierID": "mod-3",
            "movementModifier": {
              "movementModifierID": "mod-3",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "c675cae7-124c-4c4e-8e6f-eb1d50b9c8fe",
                "movementID": "dbad8f2c-d202-4ce6-8fe3-fc739366c1af",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "2bf1d9e4-3493-44ad-ac5b-f2b558a85843",
                "movementID": "dbad8f2c-d202-4ce6-8fe3-fc739366c1af",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "212f508e-a0ee-4699-916a-1fd1a0ba37ab",
                "movementID": "dbad8f2c-d202-4ce6-8fe3-fc739366c1af",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "9718772b-9724-4c12-9c36-cbc9d2ffd5db",
            "trainingSessionID": "8867785d-8984-49a2-96fe-674429c3db71",
            "movementBaseID": "base-4",
            "movementBase": {
              "movementBaseID": "base-4",
              "name": "Exercise 4"
            },
            "movementModifierID": "mod-4",
            "movementModifier": {
              "movementModifierID": "mod-4",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "ee1e14d7-8c5a-47a4-a6e5-c4d1fee41373",
                "movementID": "9718772b-9724-4c12-9c36-cbc9d2ffd5db",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "1ef3824c-2ac2-4f67-9e2b-7d87371801cb",
                "movementID": "9718772b-9724-4c12-9c36-cbc9d2ffd5db",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "0ac74d46-4809-484c-87ad-cc1b5c20e82c",
                "movementID": "9718772b-9724-4c12-9c36-cbc9d2ffd5db",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          },
          {
            "movementID": "0164ea3c-bf96-4545-a9d4-c2cc830a282b",
            "trainingSessionID": "8867785d-8984-49a2-96fe-674429c3db71",
            "movementBaseID": "base-5",
            "movementBase": {
              "movementBaseID": "base-5",
              "name": "Exercise 5"
            },
            "movementModifierID": "mod-5",
            "movementModifier": {
              "movementModifierID": "mod-5",
              "name": "Standard",
              "equipment": "Barbell",
              "duration": 2
            },
            "sets": [
              {
                "setEntryID": "266c7495-3c45-42ac-ac76-592cbbed596c",
                "movementID": "0164ea3c-bf96-4545-a9d4-c2cc830a282b",
                "recommendedReps": 5,
                "recommendedWeight": 100,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "b4600358-a5d1-4b54-9ca1-a9fe1ce8ca58",
                "movementID": "0164ea3c-bf96-4545-a9d4-c2cc830a282b",
                "recommendedReps": 5,
                "recommendedWeight": 110,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              },
              {
                "setEntryID": "c8ed1d82-97f0-4e99-b8cf-f954b0f8cae8",
                "movementID": "0164ea3c-bf96-4545-a9d4-c2cc830a282b",
                "recommendedReps": 5,
                "recommendedWeight": 120,
                "recommendedRPE": 8,
                "weightUnit": "Pounds",
                "actualReps": 0,
                "actualWeight": 0,
                "actualRPE": 0
              }
            ],
            "notes": ""
          }
        ]
      }
    ]
  }
]