using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace TP11
{
    public class CodeSmellVisitor : CSharpSyntaxVisitor
    {
        public List<double> MagicNumbersWhitelist;
        public List<string> MagicStringsWhitelist;
        public List<char> MagicCharsWhitelist;
        private readonly CsharpFileContent _fileContent;
        private readonly SyntaxTree _tree;
        private const int ParameterCountLimit = 4;
        private const int MethodLineCountLimit = 10;
        private const int BracketsCount = 2;
        private const int DefaultGetterLineCount = 5;

        public CodeSmellVisitor(CsharpFileContent fileContent)
        {
            _fileContent = fileContent;
            _tree = SyntaxHelper.ParseProgramFile(fileContent.Path);
            MagicNumbersWhitelist = new List<double>() { -1, 0, 1 };
            MagicStringsWhitelist = new List<string>() {string.Empty};
            MagicCharsWhitelist = new List<char>() {'\0', '\r', '\n'};
        }

        public CsharpFileContent AnalyzeCodeSmells()
        {
            VisitAllNodes(_tree.GetRoot());
            return _fileContent;
        }

        #region Overrides

        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            if (string.IsNullOrEmpty(_fileContent.Name))
                _fileContent.Name = node.Name.ToString();
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            CheckParameterListLength(node);
            CheckMethodSize(node);
        }

        public override void VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            CheckLiteralExpressions(node);
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            IsDataClass(node);

            if (!_fileContent.Name.Equals(String.Empty))
                _fileContent.Name += $".{node.Identifier}";
        }

        #endregion

        protected int GetNodeLineNumber(TextSpan span)
        {
            return _tree.GetLineSpan(span).StartLinePosition.Line + 1;
        }

        protected IEnumerable<T> GetIntersectedNodesInBody<T>(MethodDeclarationSyntax methodDeclarationNode)
        {
            return methodDeclarationNode.DescendantNodes().Where(
                node => node.Span.IntersectsWith(methodDeclarationNode.Body.Span)).OfType<T>();
        }

        protected void VisitAllNodes(SyntaxNode root)
        {
            VisitChildrenNodes(root);
        }

        private void VisitChildrenNodes(SyntaxNode node)
        {
            foreach (var childNode in node.ChildNodes())
            {
                Visit(childNode);

                if (childNode.ChildNodes().Any())
                {
                    VisitChildrenNodes(childNode);
                }
            }
        }

        #region Top-level calls

        private void CheckParameterListLength(MethodDeclarationSyntax methodDeclarationNode)
        {
            if (methodDeclarationNode.ParameterList.Parameters.Count > ParameterCountLimit)
            {
                var line = GetNodeLineNumber(methodDeclarationNode.Span);
                _fileContent.Smells.Add(new CsharpFileContent.CodeSmell(ECodeSmellType.LongParamList, line));
            }
        }

        private void CheckMethodSize(MethodDeclarationSyntax methodDeclarationNode)
        {
            if (methodDeclarationNode.Body.GetText().Lines.Count <= MethodLineCountLimit)
                return;

            var validLines = GetValidLines(methodDeclarationNode.Body.GetText().Lines);

            if (validLines < MethodLineCountLimit)
                return;

            _fileContent.Smells.Add(new CsharpFileContent.CodeSmell(ECodeSmellType.LongMethod, 0));
        }

        private void CheckLiteralExpressions(LiteralExpressionSyntax literalExpression)
        {
            if (!CanBeMagicAttribute(literalExpression))
                return;

            if (IsInWhitelist(literalExpression))
                return;

            var line = GetNodeLineNumber(literalExpression.Span);
            _fileContent.Smells.Add(new CsharpFileContent.CodeSmell(ECodeSmellType.MagicAttribute, line));
        }

        private void IsDataClass(ClassDeclarationSyntax classNode)
        {
            var classVariableMembers = GetClassFieldVariables(classNode);
            var classMethods = classNode.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();

            if (ContainsBehaviourMethod(classMethods, classVariableMembers))
                return;

            _fileContent.Smells.Add(new CsharpFileContent.CodeSmell(ECodeSmellType.DataClass, 0));
        }

        #endregion

        #region Low-level calls

        private int GetValidLines(TextLineCollection lines)
        {
            int validLinesCount = 0;

            foreach (TextLine line in lines)
            {
                if (IsValidLine(line))
                    validLinesCount++;
            }

            return validLinesCount;
        }

        private bool IsValidLine(TextLine line)
        {
            if (IsLineEmpty(line))
                return false;

            if (IsBracketOnly(line))
                return false;

            return true;
        }

        private bool IsLineEmpty(TextLine line)
        {
            return line.Span.Length == 0;
        }

        private bool IsBracketOnly(TextLine line)
        {
            string cachedLine = line.ToString();

            if (!(cachedLine.Contains("{")) && !(cachedLine.Contains("}")))
                return false;

            if (cachedLine.Trim().Length > BracketsCount)
                return false;

            return true;
        }

        private bool IsInWhitelist(LiteralExpressionSyntax literalExpression)
        {
            if (literalExpression.Token.IsKind(SyntaxKind.NumericLiteralToken))
            {
                foreach (double magic in MagicNumbersWhitelist)
                {
                    if (literalExpression.ToString().Equals(magic.ToString(new NumberFormatInfo())))
                        return true;
                }
            }
            else if (literalExpression.Token.IsKind(SyntaxKind.CharacterLiteralToken))
            {
                foreach (char magic in MagicCharsWhitelist)
                {
                    if (literalExpression.ToString().Equals(magic.ToString()))
                        return true;
                }
            }
            else if (literalExpression.Token.IsKind(SyntaxKind.StringLiteralToken))
            {
                foreach (string magic in MagicStringsWhitelist)
                {
                    if (literalExpression.ToString().Equals(magic))
                        return true;
                }
            }

            return false;
        }

        private bool CanBeMagicAttribute(LiteralExpressionSyntax literalExpression)
        {
            return literalExpression.Token.IsKind(SyntaxKind.NumericLiteralToken) ||
                   literalExpression.Token.IsKind(SyntaxKind.CharacterLiteralToken) ||
                   literalExpression.Token.IsKind(SyntaxKind.StringLiteralToken);
        }

        private bool ContainsBehaviourMethod(List<MethodDeclarationSyntax> methodNodes, List<VariableDeclarationSyntax> classVariableMembers)
        {
            if (HasLongMethod(methodNodes))
                return true;

            if (HasShortBehaviourMethod(methodNodes, classVariableMembers))
                return true;

            return false;
        }

        private bool HasShortBehaviourMethod(List<MethodDeclarationSyntax> methodNodes, List<VariableDeclarationSyntax> classVariableMembers)
        {
            foreach (var method in methodNodes)
            {
                if ((!IsGetter(method, classVariableMembers)) && (!IsSetter(method, classVariableMembers)))
                    return true;
            }

            return false;
        }

        private bool IsGetter(MethodDeclarationSyntax methodDeclarationNode, List<VariableDeclarationSyntax> classVariableMembers)
        {
            if (methodDeclarationNode.ReturnType.GetFirstToken().RawKind.Equals(SyntaxKind.VoidKeyword.GetHashCode()))
                return false;

            if (methodDeclarationNode.ParameterList.Parameters.Count > 0)
                return false;

            if (GetIntersectedNodesInBody<ExpressionStatementSyntax>(methodDeclarationNode).Any())
                return false;

            if (!HasSingleReturnStatement(methodDeclarationNode, out ReturnStatementSyntax returnNode))
                return false;

            if (!IsFieldMember(returnNode.Expression, classVariableMembers))
                return false;

            return true;
        }

        private bool IsSetter(MethodDeclarationSyntax methodDeclarationNode, List<VariableDeclarationSyntax> classVariableMembers)
        {
            if (methodDeclarationNode.ParameterList.Parameters.Count != 1)
                return false;

            if (GetIntersectedNodesInBody<ReturnStatementSyntax>(methodDeclarationNode).Any())
                return false;

            if (!methodDeclarationNode.ReturnType.GetFirstToken().RawKind.Equals(SyntaxKind.VoidKeyword.GetHashCode()))
                return false;

            AssignmentExpressionSyntax assignmentExpressionSyntax = null;
            int assignmentExpressionCount = 0;

            foreach (var assignmentExpression in GetIntersectedNodesInBody<AssignmentExpressionSyntax>(methodDeclarationNode))
            {
                if (!assignmentExpression.IsKind(SyntaxKind.SimpleAssignmentExpression))
                    return false;

                assignmentExpressionSyntax = assignmentExpression;
                assignmentExpressionCount++;

                if (assignmentExpressionCount > 1)
                    return false;
            }

            if (!IsFieldMember(assignmentExpressionSyntax?.Left, classVariableMembers))
                return false;

            return true;
        }

        private bool HasLongMethod(IEnumerable<MethodDeclarationSyntax> methodNodes)
        {
            foreach (var methodDeclarationSyntax in methodNodes)
            {
                if (!IsShortMethod(methodDeclarationSyntax))
                    return true;
            }

            return false;
        }

        private bool IsShortMethod(MethodDeclarationSyntax methodDeclarationNode)
        {
            return methodDeclarationNode.Body.GetText().Lines.Count <= DefaultGetterLineCount;
        }

        private bool IsFieldMember(ExpressionSyntax expressionSyntax, List<VariableDeclarationSyntax> classVariableMembers)
        {
            foreach (var member in classVariableMembers)
            {
                foreach (var variable in member.Variables)
                {
                    if (expressionSyntax.ToString().Equals(variable.Identifier.ToString()))
                        return true;
                }
            }

            return false;
        }

        private List<VariableDeclarationSyntax> GetClassFieldVariables(ClassDeclarationSyntax classNode)
        {
            var classVariableMembers = new List<VariableDeclarationSyntax>();

            foreach (var classFieldMember in classNode.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                classVariableMembers.Add(classFieldMember.Declaration);
            }

            return classVariableMembers;
        }

        private bool HasSingleReturnStatement(MethodDeclarationSyntax methodDeclarationNode, out ReturnStatementSyntax returnNode)
        { 
            var returnNodes = GetIntersectedNodesInBody<ReturnStatementSyntax>(methodDeclarationNode).ToList();
            bool hasSingleReturnStatement = returnNodes.Count == 1;
            returnNode = hasSingleReturnStatement ? returnNodes[0] : null;
            return hasSingleReturnStatement;
        }

        #endregion
    }
}
