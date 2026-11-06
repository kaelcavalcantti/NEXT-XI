// Registro de planos: id -> módulo com create(ctx) => { update(localT, globalT) }.
import { SHOTS } from '../timeline.js';
import { placeholder } from './placeholder.js';
import * as opening from './opening.js';
import * as cards from './cards.js';
import * as drive from './drive.js';
import * as houseA from './house_a.js';
import * as houseB from './house_b.js';

export const SHOT_MODULES = {};
for (const s of SHOTS) SHOT_MODULES[s.id] = placeholder(s.id);
Object.assign(SHOT_MODULES, opening.shots, cards.shots, drive.shots, houseA.shots, houseB.shots);
