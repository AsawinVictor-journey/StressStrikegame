#!/usr/bin/env python3
"""
Hand off pose classifier tasks to ChatGPT via OpenAI API.
Usage: python hand_off_to_chatgpt.py <task_name>

Tasks:
  - csv_logger    : Generate C# script to log angles to CSV
  - train_script  : Generate Python sklearn training script
  - inference     : Generate C# inference class with weights
"""

import os
import sys
import json
from pathlib import Path

try:
    import openai
except ImportError:
    print("ERROR: openai not installed. Run: pip install openai")
    sys.exit(1)

# Get API key from environment
API_KEY = os.getenv("OPENAI_API_KEY")
if not API_KEY:
    print("ERROR: OPENAI_API_KEY environment variable not set")
    print("Set it with: $env:OPENAI_API_KEY='sk-...'  (PowerShell)")
    print("            or export OPENAI_API_KEY='sk-...'  (Bash)")
    sys.exit(1)

client = openai.OpenAI(api_key=API_KEY)

SPEC = """
# Pose Classification Training Setup

## Goal
Train a logistic regression classifier to validate yoga poses in real-time.

## Input Data
- 5 features per frame: leftElbow, rightElbow, leftShoulder, rightShoulder, torsoLean (all float)
- 6 pose classes: Prayer, OpenArms, RaiseArms, ClosedArms, SideBendLeft, SideBendRight
- Expected: ~900 samples per pose from 30-second video clips at 30fps
- CSV format: leftElbow,rightElbow,leftShoulder,rightShoulder,torsoLean,pose_name

## Requirements
- No external dependencies beyond sklearn + numpy
- Inference must run in <2ms per frame
- Must handle missing frames gracefully
- Output weights in JSON format ready to paste into C#

## Deliverables Format
For the training script, output must include:
```python
print(json.dumps({
    "coef": clf.coef_.tolist(),
    "intercept": clf.intercept_.tolist(),
    "classes": list(clf.classes_)
}, indent=2))
```
"""

TASKS = {
    "csv_logger": """
You are a C# code generator for Unity.

Write a COMPLETE, STANDALONE C# script that:
1. Hooks into MediaPipePoseTracker to read the 5 joint angles every frame
2. Logs them to a CSV file in Application.persistentDataPath
3. Includes UI button to start/stop logging
4. File format: leftElbow,rightElbow,leftShoulder,rightShoulder,torsoLean,pose_name

Requirements:
- No dependencies beyond Unity
- Handle file I/O errors gracefully
- Include clear comments
- Make it copy-paste ready

Output ONLY the C# code, no explanation.
""",

    "train_script": """
You are a Python ML engineer.

Write a COMPLETE, STANDALONE Python script that:
1. Reads a CSV file with columns: leftElbow,rightElbow,leftShoulder,rightShoulder,torsoLean,pose_name
2. Trains LogisticRegression from sklearn
3. Prints accuracy and confusion matrix
4. Outputs weights in JSON format:
   {"coef": [[...], [...], ...], "intercept": [...], "classes": ["Prayer", ...]}

Requirements:
- Only use sklearn, numpy, pandas
- Handle missing values (drop rows)
- Use 80/20 train/test split
- Print classification_report
- Output JSON on last line (parseable)

Output ONLY the Python code, no explanation.
""",

    "inference": """
You are a C# developer.

Write a COMPLETE, STANDALONE C# class that:
1. Stores weights and intercept from trained LogisticRegression
2. Takes 5 floats (the joint angles) as input
3. Returns the predicted class name and confidence (0-1)
4. Runs in <2ms

The class should:
- Be copy-paste ready
- Include example usage in comments
- Handle edge cases (NaN, Infinity)
- Work in Unity without external dependencies

Output ONLY the C# code, no explanation.
"""
}

def hand_off(task_name: str):
    """Send a task to ChatGPT and return the response."""

    if task_name not in TASKS:
        print(f"ERROR: Unknown task '{task_name}'")
        print(f"Available: {', '.join(TASKS.keys())}")
        sys.exit(1)

    task_prompt = TASKS[task_name]
    full_prompt = f"SPEC:\n{SPEC}\n\nTASK:\n{task_prompt}"

    print(f"Sending '{task_name}' to ChatGPT...")
    print("-" * 60)

    try:
        response = client.chat.completions.create(
            model="gpt-3.5-turbo",
            messages=[
                {
                    "role": "system",
                    "content": "You are a code generator. Output ONLY code, no explanations or markdown fences."
                },
                {
                    "role": "user",
                    "content": full_prompt
                }
            ],
            temperature=0.2,  # Low temp for consistent code
            max_tokens=2000
        )

        code = response.choices[0].message.content.strip()

        # Save to file
        output_file = f"chatgpt_{task_name}_output.txt"
        Path(output_file).write_text(code)

        print(code)
        print("-" * 60)
        print(f"✓ Saved to: {output_file}")
        return code

    except openai.APIError as e:
        print(f"ERROR: OpenAI API failed: {e}")
        sys.exit(1)

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python hand_off_to_chatgpt.py <task>")
        print(f"Tasks: {', '.join(TASKS.keys())}")
        sys.exit(1)

    task = sys.argv[1]
    hand_off(task)
