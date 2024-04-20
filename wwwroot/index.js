let $visibleMenuRootItem = null

addEventListener(`mousedown`, e =>
{
    const $ = e.target

    if ($.classList.contains(`menu-root-item`))
    {
        toggleMenu($)
        return
    }

    if (!$visibleMenuRootItem)
        return

    if ($.classList.contains(`interactable`))
        return

    closeMenu()
})

addEventListener(`mouseover`, e =>
{
    if (!$visibleMenuRootItem)
        return

    const $ = e.target
    if (!$.classList.contains(`menu-root-item`))
        return

    if ($visibleMenuRootItem != $)
    {
        openMenu($)
        return
    }
})

function toggleMenu($)
{
    if ($visibleMenuRootItem)
        closeMenu()
    else
        openMenu($)
}

function openMenu($)
{
    closeMenu()
    $visibleMenuRootItem = $
    $.classList.add(`visible`)
}

function closeMenu()
{
    if (!$visibleMenuRootItem)
        return

    $visibleMenuRootItem.classList.remove(`visible`)
    $visibleMenuRootItem = null
}

function scrollDown($)
{
    $.scrollTo(0, $.scrollHeight)
}
