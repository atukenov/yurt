import { InputHTMLAttributes, ReactNode } from 'react';
import Icon from './Icon';
import './textfield.scss';

type TTextfield = InputHTMLAttributes<HTMLInputElement> & {
	icon?: string;
	onEnterKey?: () => void;
};

const Textfield = (props: TTextfield) => {
	const { icon, onEnterKey, className = '', ...inputProps } = props;

	const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
		if (e.key === 'Enter' && onEnterKey) {
			onEnterKey();
		}
		if (props.onKeyDown) {
			props.onKeyDown(e);
		}
	};

	return (
		<div className={`textfield ${className}`}>
			{icon && <Icon className="textfieldIcon" code={icon} />}
			<input
				{...inputProps}
				className="textfieldInput"
				onKeyDown={handleKeyDown}
			/>
		</div>
	);
};

export default Textfield;
