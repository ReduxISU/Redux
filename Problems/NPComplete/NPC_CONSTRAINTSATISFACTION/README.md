# Constraint Satisfaction

General finite-domain CSP with explicit tables of allowed tuples. Instances are JSON strings:

```json
{
  "domains": {"x": ["0", "1"], "y": ["0", "1"]},
  "constraints": [
    {"scope": ["x", "y"], "tuples": [["0", "1"], ["1", "0"]]}
  ]
}
```

The default asks for two different Boolean values. A certificate is a JSON object
such as `{"x":"0","y":"1"}`. Names and values are case-sensitive strings.
Tuple positions correspond to scope order. Constraints may have any arity; all
constraints must hold. Domains must contain distinct values, scopes must contain
distinct declared variables, and tuple values must belong to their domains.
Missing, duplicate, and unexpected schema properties are rejected.

Empty domains and empty allowed tables are valid unsatisfiable inputs. With no
constraints, any complete domain-respecting assignment works. With no variables,
the empty assignment `{}` works unless a constraint forbids it: a zero-arity
constraint with `tuples: [[]]` is true and one with `tuples: []` is false.

Malformed instances throw `ProblemParseException`; malformed certificates throw
`CertificateParseException`. Well-formed assignments with missing/extra variables,
out-of-domain values, or violated constraints return false.

The default solver exhaustively enumerates domain assignments, returns a JSON
certificate on success, `No solution exists` on exhaustion, and `timeout` when
its timer expires. The problem uses `DummyVisualization`.
