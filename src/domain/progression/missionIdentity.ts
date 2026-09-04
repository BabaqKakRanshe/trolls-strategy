import { missionRunId, type MissionRunId } from '../model';

export interface MissionRunIdentity {
  runId: MissionRunId;
  combatSeed: number;
}

export function deriveMissionRunIdentity(
  missionId: string,
  campaignSeed: number,
  sequence: number,
): MissionRunIdentity {
  if (!Number.isSafeInteger(sequence) || sequence < 1) {
    throw new RangeError('Mission run sequence must be a positive safe integer');
  }

  let value = ((campaignSeed >>> 0) ^ Math.imul(sequence, 0x9e37_79b9)) >>> 0;
  value = Math.imul(value ^ (value >>> 16), 0x21f0_aaad);
  value = Math.imul(value ^ (value >>> 15), 0x735a_2d97);

  return {
    runId: missionRunId(`${missionId}-run-${sequence}`),
    combatSeed: (value ^ (value >>> 15)) >>> 0,
  };
}
