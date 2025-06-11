// src/lib/utils/programStorage.ts

import type { Program } from '$lib/types/programs';
import { fakePrograms } from '$lib/testData/programs';

const STORAGE_KEY = 'lionheart_programs';

export function loadPrograms(): Program[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      return JSON.parse(raw) as Program[];
    } else {
      // Seed with default data if none exists
      savePrograms(fakePrograms);
      return fakePrograms;
    }
  } catch (err) {
    console.error('Failed to load programs:', err);
    return fakePrograms;
  }
}

export function savePrograms(programs: Program[]): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(programs));
  } catch (err) {
    console.error('Failed to save programs:', err);
  }
}

export function addProgram(newProgram: Program): void {
  const current = loadPrograms();
  current.push(newProgram);
  savePrograms(current);
}

export function updateProgram(updatedProgram: Program): void {
  const current = loadPrograms().map(p =>
    p.programID === updatedProgram.programID ? updatedProgram : p
  );
  savePrograms(current);
}

export function deleteProgram(id: string): void {
  const current = loadPrograms().filter(p => p.programID !== id);
  savePrograms(current);
}
