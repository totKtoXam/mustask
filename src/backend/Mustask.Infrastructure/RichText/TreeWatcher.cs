using System.Runtime.CompilerServices;
using AngleSharp.Dom;

namespace Mustask.Infrastructure.RichText;

/// <summary>
/// Watches the tree as the parser builds it, from its element hook, and stops the parse deterministically (the tree
/// is the same for the same input on every host) when what the parser has done passes a bound of
/// <see cref="RichTextGuardLimits"/>: the nesting of the element made last (read from the tree itself, so it is
/// exact), the number of elements, the sum of nesting over elements, and the foster-parenting work next to a table.
/// It also checks the stack, so a recursion nobody foresaw ends as an exception the caller answers as a server
/// error, not a dead process.
/// </summary>
/// <remarks>
/// Only child counts, <c>LastChild</c> and the child indexer are read: the DOM's sibling links are found by a linear
/// search, and using them here made a flat document quadratic.
/// </remarks>
internal sealed class TreeWatcher(RichTextGuardLimits limits)
{
    /// <summary>What a node held when it was looked at: its child count and the length of its last child when text.</summary>
    private readonly record struct Held(int Children, int TrailingText);

    private IElement? previous;
    private int created;
    private long work;
    private long insertionWork;

    private IElement? newest;
    private Held newestHeld;
    private IElement? enclosing;
    private Held enclosingHeld;

    // The open chain at the previous hook (the element made then and its ancestors) with what each held: the
    // candidates for the parent of the table made then, and what they held before it.
    private INode[] chainNodes = new INode[32];
    private Held[] chainHeld = new Held[32];
    private INode[] spareNodes = new INode[32];
    private Held[] spareHeld = new Held[32];
    private int chainCount;

    public void Created(IElement element, AngleSharp.Text.TextPosition position)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();

        if (++created > limits.MaxElements)
        {
            throw new RichTextBudgetExceededException();
        }

        if (previous is not null)
        {
            var depth = 0;
            var table = previous.LocalName == "table" ? previous : null;
            var count = 0;
            var held = spareHeld;
            var nodes = spareNodes;

            // The chain of the previous element: its depth, the nearest table, and what each holds, for the next hook.
            for (IElement? node = previous; node is not null; node = node.ParentElement)
            {
                if (count == nodes.Length)
                {
                    Array.Resize(ref nodes, count * 2);
                    Array.Resize(ref held, count * 2);
                }

                nodes[count] = node;
                held[count++] = Look(node);

                if (!ReferenceEquals(node, previous))
                {
                    depth++;

                    if (table is null && node.LocalName == "table")
                    {
                        table = node;
                    }
                }
            }

            work += depth + 1;

            if (depth > limits.MaxDomDepth || work > limits.MaxTreeWork)
            {
                throw new RichTextBudgetExceededException();
            }

            // QA D-8. Foster parenting puts text and inline content before a table, and the DOM finds the table's
            // index (linear in the parent's children) for each: per node inserted, and per character appended to the
            // text already there. While a table is the last child of its parent, whatever the parent or the text
            // before the table gains was put there for it. That is charged at the parent's size, whatever put it
            // there. Two tables are watched: the newest one made (foster content lands outside it, so the chain of
            // the element just made does not lead to it) and the nearest one the element is inside.
            if (previous.LocalName == "table")
            {
                newest = previous;
                newestHeld = BaseHeld(previous);
            }

            Charge(newest, ref newestHeld);

            if (table is not null && !ReferenceEquals(table, newest))
            {
                if (!ReferenceEquals(table, enclosing))
                {
                    enclosing = table;
                    enclosingHeld = Trailing(table);
                }

                Charge(enclosing, ref enclosingHeld);
            }

            if (insertionWork > limits.MaxInsertionWork)
            {
                throw new RichTextBudgetExceededException();
            }

            // This hook's chain becomes the previous one for the next hook; the older buffers are reused.
            spareNodes = chainNodes;
            spareHeld = chainHeld;
            chainNodes = nodes;
            chainHeld = held;
            chainCount = count;
        }

        previous = element;
    }

    private static Held Look(INode node) =>
        new(node.ChildNodes.Length, node.LastChild is IText text ? text.Data.Length : 0);

    /// <summary>
    /// What the parent of a table just made held before the table: from the previous hook's chain (the table's parent
    /// is the element open then or one of its ancestors), the table itself then added to its children.
    /// </summary>
    private Held BaseHeld(IElement table)
    {
        var holder = table.Parent;

        if (holder is null)
        {
            return default;
        }

        for (var index = 0; index < chainCount; index++)
        {
            if (ReferenceEquals(chainNodes[index], holder))
            {
                return new Held(chainHeld[index].Children + 1, chainHeld[index].TrailingText);
            }
        }

        return Trailing(table);
    }

    /// <summary>The parent's child count and the length of the text node right before <paramref name="table"/>.</summary>
    private static Held Trailing(IElement table)
    {
        if (table.Parent is not { } holder)
        {
            return default;
        }

        return new Held(holder.ChildNodes.Length, TextBefore(holder, table));
    }

    /// <summary>The text node before the last child, by index (the sibling link would be a linear search).</summary>
    private static int TextBefore(INode holder, IElement lastChild)
    {
        var length = holder.ChildNodes.Length;

        return ReferenceEquals(holder.LastChild, lastChild) && length >= 2 && holder.ChildNodes[length - 2] is IText text
            ? text.Data.Length
            : 0;
    }

    private void Charge(IElement? table, ref Held seen)
    {
        if (table?.Parent is not { } holder)
        {
            return;
        }

        var length = holder.ChildNodes.Length;

        // Something appended after the table (it is closed) is not foster parenting: only a table that is still
        // the last child can have gained nodes or text before it.
        if (ReferenceEquals(holder.LastChild, table))
        {
            var text = TextBefore(holder, table);

            if (length > seen.Children)
            {
                insertionWork += (long)(length - seen.Children) * length;
            }

            if (text > seen.TrailingText)
            {
                insertionWork += (long)(text - seen.TrailingText) * length;
            }

            seen = new Held(length, text);
        }
        else
        {
            seen = new Held(length, 0);
        }
    }
}
