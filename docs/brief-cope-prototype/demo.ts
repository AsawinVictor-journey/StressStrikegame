import { COPE_QUESTIONS, RESPONSE_SCALE, CopeAnswers } from "./briefCopeData";
import { recommendGameMode } from "./gameModeRecommendation";

console.log("=== Brief COPE — 28 Questions ===\n");
for (const q of COPE_QUESTIONS) {
  console.log(`${q.id}. [${q.subscale}] ${q.text}`);
}

console.log("\n=== Response scale ===");
for (const r of RESPONSE_SCALE) {
  console.log(`${r.value} = ${r.label}`);
}

// Example answers: a player who copes mostly through active coping + planning.
const sampleAnswers: CopeAnswers = Object.fromEntries(
  COPE_QUESTIONS.map((q) => [q.id, 2])
) as CopeAnswers;
sampleAnswers[2] = 4; // Active coping
sampleAnswers[7] = 4;
sampleAnswers[14] = 4; // Planning
sampleAnswers[25] = 4;

const recommendation = recommendGameMode(sampleAnswers);

console.log("\n=== Sample recommendation ===");
console.log(`Mode: ${recommendation.modeName}`);
console.log(`Coach says: ${recommendation.coachMessage}`);
