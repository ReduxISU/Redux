
using API.Problems.NPComplete.NPC_SAT3;

class SAT3PQObject {
    public SAT3 SATState;
    public int depth;
    public int totalVars;
    public Dictionary<string, int> varWeights;
    public Dictionary<string, bool> varStates;
    public string nextVar;

    public SAT3PQObject(SAT3 inSAT3, int newDepth, int totalVariables) {
        // SATState must NOT alias the caller's SAT3 instance: this constructor removes
        // the "next" literal from SATState.literals below, which is expected internal
        // bookkeeping as the recursive backtracking algorithm descends -- but if
        // SATState were the exact object passed in, that RemoveAt would also mutate the
        // caller's own SAT3.literals as a side effect of solve(). Clone instead, so the
        // caller's SAT3 (and its literals list) is left untouched.
        SATState = CloneSat3(inSAT3);
        depth = newDepth;
        totalVars = totalVariables;
        varWeights = new Dictionary<string, int>();
        varStates = new Dictionary<string, bool>();

        //gets the next variable for evaluation and removes the variable from the literals
        string newVar = getVarFromLiteral(SATState.literals[0]);
        SATState.literals.RemoveAt(0);
        nextVar = newVar;
    }

    //builds an independent copy of a SAT3 instance (own clauses/literals lists) so that
    //mutating the copy (e.g. literals.RemoveAt, clauses reassignment) never affects the
    //original instance the caller passed in.
    private static SAT3 CloneSat3(SAT3 source) {
        SAT3 clone = new SAT3();
        clone.instance = source.instance;
        clone.clauses = source.clauses.Select(clause => new List<string>(clause)).ToList();
        clone.literals = new List<string>(source.literals);
        return clone;
    }

    //gets the variable witht he highest occurance

    private int getHighestVal() {
        string highVar = string.Empty;
        int highVal = 0;
        foreach (KeyValuePair<string, int> kvp in this.varWeights) {
            if (kvp.Value > highVal) {
                highVal = kvp.Value;
                highVar = kvp.Key;
            }
        }
        // Console.WriteLine("highVar is : " + highVar);
        return highVal;
    }

    private void setVarStates(Dictionary<string, bool> newVarStates) {
        Dictionary<string, bool> copyDict = new Dictionary<string, bool>();
        foreach (KeyValuePair<string, bool> kvp in newVarStates) {
            copyDict.Add(kvp.Key, kvp.Value);
        }
        this.varStates = copyDict;
    }

    private void setVarWeights(Dictionary<string, int> newVarWeights) {
        Dictionary<string, int> copyDict = new Dictionary<string, int>();
        foreach (KeyValuePair<string, int> kvp in newVarWeights) {
            copyDict.Add(kvp.Key, kvp.Value);
        }
        this.varWeights = copyDict;
    }

    public int getPQWeight() {
        return this.totalVars - this.depth - getHighestVal();

    }

    //Returns the two sats 
    public List<SAT3PQObject> createSATChildren(int depth, int totalNumberOfVariables) {
        // string var = this.varPQ.Dequeue();
        List<SAT3PQObject> outList = new List<SAT3PQObject>();
        // createNewSatState returns null for an unviable state — skip those.
        SAT3PQObject? trueChild = createNewSatState(true, depth, totalNumberOfVariables);
        if (trueChild != null) outList.Add(trueChild);
        SAT3PQObject? falseChild = createNewSatState(false, depth, totalNumberOfVariables);
        if (falseChild != null) outList.Add(falseChild);
        return outList;
    }

    //Takes in a variable and a boolean value and returns the new SATState
    //Returns null if the new state would be unviable
    private SAT3PQObject? createNewSatState(bool boolValue, int depth, int totalNumberOfVariables) { //This is the meat and potatoes of the solver
                                                                                                     //update the variables to the string representation of the boolean value
                                                                                                     //pass the modified clause to the evaluateBooleanExpression method
                                                                                                     //if the evaluation returns a viable expression it will return a string else it will returns null
                                                                                                     //return the result

        // Console.WriteLine("nextVar at run is : " + this.nextVar);


        //create a new phiInput
        //Then construct a new SAT3 with that phi input
        string newPhiExpression = "(";
        string tempExpression = "";
        string expLiteral;
        bool isValid = true;

        //THIS IS THE CODE THAT PROCESSES THE CREATION OF THE NEW STATE
        //THIS DOES NOT CHECK SATISFIABILITY
        //Iterates through each clause evaluating and creating the new state and writting it to the appropriate out list
        foreach (List<string> boolExp in this.SATState.clauses) {
            tempExpression = "";
            //eval code
            foreach (string expVar in boolExp) {
                // Console.WriteLine(expVar);
                expLiteral = getVarFromLiteral(expVar); //expVar[expVar.Length - 1].ToString();
                                                        //if the var
                if (expLiteral.Equals(this.nextVar)) { // expVar.Contains(this.nextVar)
                                                       //checks if the boolean value returns true, if so accepts the expression and breaks the loop
                    if (expVar.Length > 0) {
                        if ((expVar[0] == '!' && boolValue == false) || (expVar[0] != '!' && boolValue == true)) {
                            //UPDATE HM
                            updateVarWeights(boolExp, this.nextVar);
                            tempExpression = string.Empty;
                            break; //Exit the foreach as the clause has been satisfied
                        }
                        //if the variable is the last literal the clause is invalid
                        else if (boolExp.Count == 1) {
                            isValid = false;
                        }
                    }
                    // else{
                    //     Console.WriteLine("empty literal found");
                    //     //Does an empty literal mean an invalid expression?
                    // }

                }
                //if the tempExpression is longer than 0 then add the conditional OR statement
                else {
                    if (tempExpression.Length > 0) {
                        tempExpression += "|" + expVar;
                    } else {
                        tempExpression += expVar;
                    }
                }
            }
            //if expression was not satisfied adds the modified expression to the phi statement
            if (tempExpression != string.Empty) {
                //adds the AND clause inbetween statements
                if (newPhiExpression.EndsWith(")")) {
                    newPhiExpression += "&";
                }
                //adds the temp expression
                newPhiExpression += "(" + tempExpression + ")";
            }
        }
        newPhiExpression += ")";
        // Console.WriteLine(this.nextVar + " : " + boolValue.ToString());
        // Console.WriteLine(newPhiExpression);

        //update dictionary
        // this.varStates.Add(this.nextVar, boolValue);
        if (isValid) {
            SAT3PQObject newSATObj;
            if (newPhiExpression == "()") {
                // All clauses were satisfied by assigning nextVar = boolValue.
                // new SAT3("()") is rejected by validateInstance (empty literal), so build
                // the solved-sentinel state directly: clauses = [[""]] is what
                // evaluateBooleanExpression checks to return 1 (satisfied).
                SAT3 doneSAT = new SAT3();
                doneSAT.clauses = new List<List<string>> { new List<string> { "" } };
                newSATObj = new SAT3PQObject(doneSAT, depth + 1, totalNumberOfVariables);
            } else {
                newSATObj = new SAT3PQObject(new SAT3(newPhiExpression), depth + 1, totalNumberOfVariables);
            }
            newSATObj.setVarStates(this.varStates);
            newSATObj.setVarWeights(this.varWeights);
            //adds the new state to the objects state of vars
            // if(this.nextVar != null){
            //adds var to the next state
            if (!newSATObj.varStates.ContainsKey(this.nextVar)) {
                newSATObj.varStates.Add(this.nextVar, boolValue);
            } else {
                // Console.WriteLine("The variable : " + this.nextVar + " already exists in seen variables");
                // Console.WriteLine("the variable is set to : " + this.varStates.GetValueOrDefault(this.nextVar).ToString());
                // Console.WriteLine("The system is currently evaulating it at : " + boolValue.ToString());
            }
            return newSATObj;
        } else {
            return null;
        }
    }

    //updates the dictionary that holds the variable weights
    public void updateVarWeights(List<string> exp, string var) {
        int itemWeight;
        string tVar;
        foreach (string expVar in exp) {
            tVar = getVarFromLiteral(expVar);//expVar[expVar.Length -1].ToString();

            if (!tVar.Equals(var)) {
                itemWeight = this.varWeights.GetValueOrDefault(expVar);// TryGetValue(expVar);
                this.varWeights.Remove(expVar);
                this.varWeights.Add(expVar, itemWeight - 1);
            }
        }
    }

    //code that generates the variable priority queue
    //modifies the priority value so it sorts high to low
    public void initVarWeights() {
        Dictionary<string, int> numbVars = new Dictionary<string, int>();
        int tempCount;
        string literalKey;

        foreach (string literal in this.SATState.literals) {
            literalKey = getVarFromLiteral(literal);
            if (!numbVars.ContainsKey(literalKey)) {
                numbVars.Add(literalKey, 1);
            } else {//increments value
                    //we must remove then re-enter the value
                tempCount = numbVars.GetValueOrDefault(literalKey) + 1;
                numbVars.Remove(literalKey);
                numbVars.Add(literalKey, tempCount);
            }
        }
        // Console.WriteLine("weights initialized");
        //sets the variable weights to the newly created dictionary with their count
        setVarWeights(numbVars);
    }

    //returns the literal without the !
    private string getVarFromLiteral(string literal) {
        if (literal.StartsWith('!')) {
            return literal.Substring(1);
        }
        return literal;
    }

    //Removes all duplicates from clauses
    //ran only on the initial expression in SkeletonSolver.solve
    public void removeDuplicatesFromClauses() {
        List<List<string>> clausesWithoutDups = new List<List<string>>();
        List<string> tempList;
        HashSet<string> seen = new HashSet<string>();
        foreach (List<string> clause in this.SATState.clauses) {
            seen.Clear();
            foreach (string literal in clause) {
                if (!seen.Contains(literal)) {
                    seen.Add(literal);
                }
                // else{
                //     Console.WriteLine("duplicate found : " + literal);
                // }
            }
            tempList = seen.ToList();
            // foreach(string s in tempList){
            //     Console.Write(s + " ");
            // }
            // Console.WriteLine();
            clausesWithoutDups.Add(tempList);
        }
        this.SATState.clauses = clausesWithoutDups;
    }
}