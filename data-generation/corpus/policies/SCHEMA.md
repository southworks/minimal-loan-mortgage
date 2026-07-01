# 08 Policy RAG Schema

Documents in this folder can remain as `.txt`, but they should follow a minimal structure so retrieval and rerank can recover rules and decisions.

## Sample of required content blocks

```txt
Policy Ref: UW-100
Rule: approve if credit score >= 700
Threshold: 700
Exception: none
Action: approve
```

## Required fields in content

- `Policy Ref`
- `Rule`
- `Threshold`
- `Action`

## Optional with real MVP value

- `Exception`
