# General Finite-Domain Constraint Satisfaction

Find values for a set of variables so that every constraint is satisfied. Each variable has a finite list of allowed integer or decimal values. The solver returns a solution, and the verifier checks a supplied solution independently.

## Input format

Provide an instance as `(X,D,C)`:

- `X`: variable names in parentheses.
- `D`: one domain in braces per variable, in the same order.
- `C`: constraints in double quotes, enclosed in braces. Every constraint must hold.

```text
((x,y,z),({1,2},{2,5},{3,4}),{"x*y=10","y/x>=2","(x+y)*z!=20"})
```

Here, `x` can be `1` or `2`, `y` can be `2` or `5`, and `z` can be `3` or `4`. One valid solution is `(x:2, y:5, z:3)`.

Variable names start with an ASCII letter or underscore, followed by letters, digits, or underscores. Names are case sensitive and distinct. Each domain explicitly lists its values, contains at least one value, and has no numeric duplicates.

## Allowed constraints

Each constraint contains **one comparison or membership test**. Its operands can combine variables, integer or decimal constants, arithmetic, and parentheses.

| Operation | Allowed syntax | Example constraint |
| --- | --- | --- |
| Addition | `+` | `x+y=7` |
| Subtraction | `-` | `y-x=3` |
| Multiplication | `*` | `x*y=10` |
| Division | `/` | `y/x>=2` |
| Unary signs | `+x`, `-x`, repeated signs | `-(x+y)<0` |
| Arithmetic grouping | `(expression)` | `(x+y)*z!=20` |
| Equal | `=`, `==` | `x+y==7` |
| Not equal | `!=` | `x!=y` |
| Greater than | `>` | `y>x` |
| Greater than or equal | `>=` | `y>=x+1` |
| Less than | `<` | `x<y` |
| Less than or equal | `<=` | `x+y*2<=15` |
| Set membership | `in {values}` | `x in {1,2.5,y+1}` |
| Set non-membership | `not in {values}` | `x not in {0,y-1}` |

Unary signs are evaluated first, followed by multiplication/division, then addition/subtraction. Binary operations at the same precedence are evaluated left to right. Parentheses override this order.

### Range membership

Square brackets include an endpoint; parentheses exclude it.

| Range | Membership | Non-membership | Included endpoints |
| --- | --- | --- | --- |
| Closed | `x in [1,10]` | `x not in [1,10]` | Both |
| Open | `x in (1,10)` | `x not in (1,10)` | Neither |
| Lower included | `x in [1,10)` | `x not in [1,10)` | Lower only |
| Upper included | `x in (1,10]` | `x not in (1,10]` | Upper only |

Membership supports decimal values and arithmetic on the left, in set elements, and in range endpoints:

```text
x/10 in [.2,.5)
x+y in {x*y,7}
x in [y-z,y/z+1)
```

Use lower bounds less than or equal to upper bounds. Equal endpoints include a value only when both ends are closed. Empty sets are accepted: `x in {}` is false and `x not in {}` is true. The keywords `in` and `not` are case insensitive.

Write multiple conditions as separate quoted constraints. For example, `{"x<y","y<z"}` requires both comparisons; `{"x!=y","x!=z","y!=z"}` requires three distinct values.

## Integer and decimal values

Domains, expressions, and certificates use the same decimal arithmetic. Numbers use a decimal point and may have a leading sign: `2`, `-3`, `1.25`, `.5`, `+2.00`. Input values must be exactly representable by .NET `decimal`; arithmetic results follow its decimal precision and rounding rules. Numeric equality treats `1` and `1.0` as equal.

```text
((x,y),({.1,.2},{.2,.3}),{"x+y=.5","x not in [0,.1]"})
```

A valid solution is `(x:.2, y:.3)`.

## Solving

| Solver | How it finds a solution |
| --- | --- |
| `ConstraintSatisfactionBruteForce` | Tries complete combinations of domain values. |
| `ConstraintSatisfactionBacktracking` | Assigns variables in input order and abandons assignments that violate a ready constraint. |
| `ConstraintSatisfactionIntelligent` | Selects the variable with the fewest remaining choices and, by default, checks future domain values before continuing. |

Set the intelligent solver's `ForwardChecking` property to `false` to select variables by their original domain sizes while checking constraints after assignments. During backtracking, a constraint is evaluated once all its referenced variables have values.

A successful solve returns a certificate such as `(x:2, y:5, z:3)`. Exhausted search returns `{}`. A timed-out search returns `SolveRun.TimedOutAnswer`. Redux's `ISolver.Run` manages the time limit.

## Certificates and verification

Use a parenthesized list of `name:value` assignments:

```text
(x:2, y:5, z:3)
```

Assign every variable exactly once using a value from its domain. Assignment order is flexible, and spaces around delimiters are accepted. For an instance with zero variables, use `()`.

| Verifier outcome | Meaning |
| --- | --- |
| `true` | The certificate is valid and satisfies every constraint. |
| `false` | The certificate is valid, but at least one constraint fails. |
| `CertificateParseException` | The certificate has invalid syntax, missing/duplicate/unknown variables, invalid numbers, or out-of-domain values. |
| `ProblemParseException` | The instance or a constraint is invalid. |
| Evaluation exception | Arithmetic fails, such as division by zero, overflow, or reversed range bounds. These errors also stop a solver when encountered. |

For `((x,y),({1,2},{1,2}),{"x!=y"})`, `(x:1,y:2)` returns `true`, `(x:1,y:1)` returns `false`, and `(x:1)` throws `CertificateParseException`.

