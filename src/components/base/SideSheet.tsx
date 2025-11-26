import { ReactNode } from 'react';

import clsx from 'clsx';
import { FaTimes } from 'react-icons/fa';

import './sideSheet.scss';

const SideSheet = (props: SideSheetProps) => {
	const { children, className, title, open, setOpen } = props;
	const classList = clsx('sideSheet', open && 'sideSheetOpen', className);

	return (
		<div className={classList}>
			<div className='backdrop' onClick={() => setOpen(false)} />
			<div className='sideContainer'>
			<div className='sheetHeader'>
				<h1 className='title'>{title[0]} <span>{title[1]}</span></h1>
				<button className='xButton closeButton' onClick={() => setOpen(false)}><FaTimes /></button>
			</div>
				<div className='sheetContent'>{children}</div>
			</div>
		</div>
	);
};

export default SideSheet;

type SideSheetProps = {
	children: ReactNode,
	className?: string,
	title: string[],
	open: boolean,
	setOpen: (open: boolean) => void
}
