#!/usr/bin/env bash
set -euo pipefail

matrix="${1:-}"
kind="${2:-}"

if [[ -z "$matrix" || -z "$kind" ]]; then
  echo "Usage: $0 <matrix.md> <injection|update|telemetry>" >&2
  exit 64
fi

if [[ "$kind" != "injection" && "$kind" != "update" && "$kind" != "telemetry" ]]; then
  echo "Matrix kind must be injection, update, or telemetry: $kind" >&2
  exit 64
fi

if [[ ! -f "$matrix" ]]; then
  echo "Manual matrix is missing: $matrix" >&2
  exit 1
fi

awk -F'|' -v kind="$kind" -v matrix="$matrix" '
function trim(value) {
  gsub(/^[ \t]+|[ \t]+$/, "", value)
  return value
}

function fail(message) {
  print message > "/dev/stderr"
  failures++
}

function is_date(value) {
  return value ~ /[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]/
}

function has_evidence(value) {
  value = trim(value)
  return value != "" && value != "-" && value !~ /Requires / && value !~ /Pending / && tolower(value) !~ /not tested/
}

/^\|/ {
  id = trim($2)
  if (kind == "injection") {
    if (id !~ /^[0-9]+$/) {
      next
    }
    status = trim($10)
    verified_by = trim($11)
    date = trim($12)
    notes = trim($13)

    if (status == "PENDING" || status == "DEFERRED" || status == "FAIL") {
      fail(matrix ": row " id " is not accepted: " status)
    } else if (status != "PASS") {
      fail(matrix ": row " id " has invalid status: " status)
    } else {
      for (column = 4; column <= 8; column++) {
        if (!has_evidence($column)) {
          fail(matrix ": row " id " is PASS but verification column " column " is empty")
        }
      }
      if (!has_evidence(verified_by)) {
        fail(matrix ": row " id " is PASS but Verified By is missing")
      }
      if (!is_date(date)) {
        fail(matrix ": row " id " is PASS but Date is missing or not YYYY-MM-DD")
      }
      if (!has_evidence(notes)) {
        fail(matrix ": row " id " is PASS but Notes evidence is missing")
      }
    }
    rows++
  } else if (kind == "update") {
    if (id !~ /^UPDATE-CANARY-[0-9]+$/) {
      next
    }
    status = trim($10)
    tester_date = trim($11)
    notes = trim($12)

    if (status == "PENDING" || status == "DEFERRED" || status == "FAIL") {
      fail(matrix ": row " id " is not accepted: " status)
    } else if (status != "PASS") {
      fail(matrix ": row " id " has invalid status: " status)
    } else {
      if (!has_evidence(tester_date) || !is_date(tester_date)) {
        fail(matrix ": row " id " is PASS but Tester/date is missing or lacks YYYY-MM-DD")
      }
      if (!has_evidence(notes)) {
        fail(matrix ": row " id " is PASS but Notes evidence is missing")
      }
    }
    rows++
  } else if (kind == "telemetry") {
    if (id !~ /^TELEMETRY-[0-9]+$/) {
      next
    }
    status = trim($6)
    tester_date = trim($7)
    notes = trim($8)

    if (status == "PENDING" || status == "DEFERRED" || status == "FAIL") {
      fail(matrix ": row " id " is not accepted: " status)
    } else if (status != "PASS") {
      fail(matrix ": row " id " has invalid status: " status)
    } else {
      if (!has_evidence(tester_date) || !is_date(tester_date)) {
        fail(matrix ": row " id " is PASS but Tester/date is missing or lacks YYYY-MM-DD")
      }
      if (!has_evidence(notes)) {
        fail(matrix ": row " id " is PASS but Notes evidence is missing")
      }
    }
    rows++
  }
}

END {
  if (rows == 0) {
    fail(matrix ": no " kind " matrix rows found")
  }
  exit failures ? 1 : 0
}
' "$matrix"

echo "Manual matrix is accepted: $matrix"
