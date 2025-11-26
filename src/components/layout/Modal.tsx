import { ReactNode } from 'react';

import clsx from 'clsx';
import { FaTimes } from 'react-icons/fa';

import './modal.scss';

const Modal = (props: TModal) => {
	const { children, open, setOpen, closeIcon = true } = props;
	const classList = clsx(
		'modal',
		open && 'open',
	);

	return (
		<div className={classList}>
			<div className='backdrop' onClick={() => setOpen(false)} />
			<div className='modalPane'>
				{children}
				{ closeIcon && <button className='closeModal xButton' onClick={() => setOpen(false)}><FaTimes /></button> }
			</div>
		</div>
	);
};

export default Modal;

type TModal = {
	open: boolean;
	closeIcon?: boolean;
	setOpen: (open: boolean) => void
	children: ReactNode;
}
